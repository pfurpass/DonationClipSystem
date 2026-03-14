using DonationClipSystem.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Websocket.Client;

namespace DonationClipSystem.Services
{
    /// <summary>
    /// Verbindet zu BEIDEN StreamElements Endpoints:
    ///  1. wss://astro.streamelements.com       → echte Spenden (channel.tips)
    ///  2. wss://realtime.streamelements.com    → Test/Emulate Events (event:test)
    /// </summary>
    public class StreamElementsService : IDisposable
    {
        private readonly string _jwt;
        private WebsocketClient? _astroClient;
        private WebsocketClient? _realtimeClient;
        private bool _disposed;
        private string? _channelId;
        private bool _realtimeAuthenticated;

        public event Action<DonationEvent>? DonationReceived;
        public event Action<string>?        LogMessage;

        private const string AstroUrl    = "wss://astro.streamelements.com";
        private const string RealtimeUrl = "wss://realtime.streamelements.com/socket.io/?transport=websocket&EIO=3";
        private const string ApiBase     = "https://api.streamelements.com/kappa/v2";

        public StreamElementsService(string jwt) => _jwt = jwt;

        public async Task ConnectAsync()
        {
            _channelId = ExtractChannelIdFromJwt(_jwt);
            if (string.IsNullOrEmpty(_channelId))
                _channelId = await FetchChannelIdAsync();
            if (string.IsNullOrEmpty(_channelId))
                throw new Exception("Cannot read Channel-ID. Token correct?");

            Log($"[SE] Channel-ID: {_channelId}");
            await Task.WhenAll(ConnectAstroAsync(), ConnectRealtimeAsync());
        }

        // ─── Astro (echte Spenden) ────────────────────────────────────────────

        private async Task ConnectAstroAsync()
        {
            _astroClient = new WebsocketClient(new Uri(AstroUrl))
            {
                ReconnectTimeout      = TimeSpan.FromSeconds(30),
                ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
            };
            _astroClient.ReconnectionHappened.Subscribe(info =>
            {
                Log($"[SE-Astro] Verbunden ({info.Type})");
                SubscribeAstroTips();
            });
            _astroClient.DisconnectionHappened.Subscribe(info =>
                Log($"[SE-Astro] Getrennt: {info.Type}"));
            _astroClient.MessageReceived.Subscribe(HandleAstroMessage);
            await _astroClient.Start();
        }

        private void SubscribeAstroTips()
        {
            _astroClient?.Send(JsonConvert.SerializeObject(new
            {
                type  = "subscribe",
                nonce = Guid.NewGuid().ToString(),
                data  = new { topic = "channel.tips", room = _channelId, token = _jwt, token_type = "jwt" }
            }));
        }

        private void HandleAstroMessage(ResponseMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Text)) return;
            try
            {
                var json  = JObject.Parse(msg.Text);
                string? type  = json["type"]?.ToString();
                string? topic = json["topic"]?.ToString();

                if (type == "response")
                {
                    string? err = json["error"]?.ToString();
                    Log(string.IsNullOrEmpty(err)
                        ? $"[SE-Astro] ✓ Subscribed: {json["data"]?["topic"]}"
                        : $"[SE-Astro] Error: {err}");
                }
                else if (type == "message" && topic == "channel.tips")
                {
                    var d = json["data"]?["donation"] ?? json["data"];
                    FireDonation(d);
                }
            }
            catch (Exception ex) { Log($"[SE-Astro] Parse error: {ex.Message}"); }
        }

        // ─── Realtime / Socket.IO (Test & Emulate) ───────────────────────────

        private async Task ConnectRealtimeAsync()
        {
            _realtimeClient = new WebsocketClient(new Uri(RealtimeUrl))
            {
                ReconnectTimeout      = TimeSpan.FromSeconds(30),
                ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
            };
            _realtimeClient.ReconnectionHappened.Subscribe(info =>
            {
                Log($"[SE-Realtime] Verbunden ({info.Type})");
                _realtimeAuthenticated = false;
                // Sofort auth senden – manche Server schicken kein "40"
                Task.Delay(500).ContinueWith(_ => AuthenticateRealtime());
            });
            _realtimeClient.DisconnectionHappened.Subscribe(info =>
            {
                Log($"[SE-Realtime] Getrennt: {info.Type}");
                _realtimeAuthenticated = false;
            });
            _realtimeClient.MessageReceived.Subscribe(HandleRealtimeMessage);
            await _realtimeClient.Start();
            Log("[SE-Realtime] Connecting (for test events)...");
        }

        private void HandleRealtimeMessage(ResponseMessage msg)
        {
            if (msg.Text == null) return;
            string text = msg.Text;

            // Engine.IO / Socket.IO Protokoll
            if (text == "2") { _realtimeClient?.Send("3"); return; }  // heartbeat

            // Engine.IO open packet → Socket.IO connect kommt gleich
            if (text.StartsWith("0{"))
            {
                Log("[SE-Realtime] Engine.IO handshake ok");
                return;
            }

            // Socket.IO connect packet
            if (text == "40" || text.StartsWith("40{"))
            {
                Log("[SE-Realtime] Socket.IO connected → Authenticating...");
                AuthenticateRealtime();
                return;
            }

            if (!text.StartsWith("42")) return;

            try
            {
                var arr  = JArray.Parse(text[2..]);
                string ev = arr[0].ToString();
                var data  = arr.Count > 1 ? arr[1] : null;

                Log($"[SE-Realtime] Event: {ev}");

                switch (ev)
                {
                    case "authenticated":
                        _realtimeAuthenticated = true;
                        Log("[SE-Realtime] ✓ Authentifiziert — receiving test events");
                        break;

                    case "unauthorized":
                        Log("[SE-Realtime] ✗ Unauthorized — wrong token?");
                        break;

                    case "event":
                    case "event:test":
                        Log($"[SE-Realtime] Tip-Event: {ev}");
                        HandleRealtimeTip(data);
                        break;
                }
            }
            catch (Exception ex) { Log($"[SE-Realtime] Parse error: {ex.Message}"); }
        }

        private void AuthenticateRealtime()
        {
            if (_realtimeAuthenticated) return;
            var payload = new JObject
            {
                
                ["token"] = _jwt, ["method"] = "jwt"
            };
            string packet = "42" + new JArray("authenticate", payload).ToString(Formatting.None);
            Log($"[SE-Realtime] Sending auth packet");
            _realtimeClient?.Send(packet);
        }

        private void HandleRealtimeTip(JToken? data)
        {
            if (data == null) return;
            string? type = data["type"]?.ToString();
            if (type != "tip") { Log($"[SE-Realtime] Not a tip: type={type}"); return; }
            var d = data["data"] ?? data;
            FireDonation(d);
        }

        // ─── Gemeinsam ────────────────────────────────────────────────────────

        private void FireDonation(JToken? d)
        {
            if (d == null) return;
            decimal amount   = d["amount"]?.Value<decimal>()
                            ?? d["data"]?["amount"]?.Value<decimal>() ?? 0;
            string  donor    = d["username"]?.ToString()
                            ?? d["user"]?["username"]?.ToString()
                            ?? d["data"]?["username"]?.ToString() ?? "Anonymous";
            string  message  = d["message"]?.ToString()
                            ?? d["data"]?["message"]?.ToString() ?? string.Empty;
            string  currency = d["currency"]?.ToString()
                            ?? d["data"]?["currency"]?.ToString() ?? "EUR";

            Log($"[SE] 💰 {amount} {currency} von {donor} | {message}");
            DonationReceived?.Invoke(new DonationEvent
            {
                DonorName  = donor,
                Amount     = amount,
                Currency   = currency,
                Message    = message,
                YouTubeUrl = YouTubeHelper.FindYouTubeUrlInText(message)
            });
        }

        private static string? ExtractChannelIdFromJwt(string jwt)
        {
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return null;
                string p = parts[1].Replace('-', '+').Replace('_', '/');
                int pad = p.Length % 4; if (pad != 0) p += new string('=', 4 - pad);
                var obj = JObject.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p)));
                return obj["channel"]?["_id"]?.ToString() ?? obj["_id"]?.ToString() ?? obj["channel"]?.ToString();
            }
            catch { return null; }
        }

        private async Task<string?> FetchChannelIdAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_jwt}");
                var obj = JObject.Parse(await http.GetStringAsync($"{ApiBase}/channels/me"));
                return obj["_id"]?.ToString();
            }
            catch (Exception ex) { Log($"[SE] API-Error: {ex.Message}"); return null; }
        }

        private void Log(string msg) => LogMessage?.Invoke(msg);

        public void Disconnect()
        {
            _astroClient?.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "disconnect");
            _realtimeClient?.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "disconnect");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _astroClient?.Dispose();
            _realtimeClient?.Dispose();
        }
    }
}
