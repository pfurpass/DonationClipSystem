using DonationClipSystem.Models;
using Newtonsoft.Json.Linq;
using Websocket.Client;

namespace DonationClipSystem.Services
{
    /// <summary>
    /// Tipestream Socket.IO Verbindung.
    /// Doku: https://api.tipeeestream.com/api-doc/socketio
    ///
    /// Ablauf:
    ///   1. GET https://api.tipeeestream.com/v2.0/site/socket  → aktuellen Host/Port holen
    ///   2. WS connect mit ?access_token=API_KEY
    ///   3. join-room emit
    ///   4. new-event listener
    /// </summary>
    public class TipestreamService : IDisposable
    {
        private readonly string _apiKey;
        private WebsocketClient? _client;
        private bool _disposed;

        public event Action<DonationEvent>? DonationReceived;
        public event Action<string>?        LogMessage;

        public TipestreamService(string apiKey) => _apiKey = apiKey;

        public async Task ConnectAsync()
        {
            // 1) Aktuellen Socket-Host von API holen
            string socketHost = await GetSocketHostAsync();
            Log($"[Tipestream] Socket-Host: {socketHost}");

            // 2) WebSocket URL mit access_token
            string wsUrl = $"{socketHost}/socket.io/?transport=websocket&access_token={Uri.EscapeDataString(_apiKey)}";

            _client = new WebsocketClient(new Uri(wsUrl))
            {
                ReconnectTimeout      = TimeSpan.FromSeconds(30),
                ErrorReconnectTimeout = TimeSpan.FromSeconds(10)
            };

            _client.ReconnectionHappened.Subscribe(info =>
                Log($"[Tipestream] Verbunden ({info.Type})"));

            _client.DisconnectionHappened.Subscribe(info =>
                Log($"[Tipestream] Getrennt: {info.Type}"));

            _client.MessageReceived.Subscribe(HandleMessage);

            await _client.Start();
            Log("Connecting to Tipestream...");
        }

        private static async Task<string> GetSocketHostAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                string json = await http.GetStringAsync("https://api.tipeeestream.com/v2.0/site/socket");
                var obj  = JObject.Parse(json);
                string host = obj["datas"]?["host"]?.ToString() ?? "https://sso-cf.tipeeestream.com";
                string port = obj["datas"]?["port"]?.ToString() ?? "443";
                // https → wss
                host = host.Replace("https://", "wss://").Replace("http://", "ws://");
                return $"{host}:{port}";
            }
            catch
            {
                // Fallback
                return "wss://sso-cf.tipeeestream.com:443";
            }
        }

        private void HandleMessage(ResponseMessage msg)
        {
            if (msg.Text == null) return;
            string text = msg.Text;

            // Socket.IO Engine.IO Protokoll
            if (text == "2")           { _client?.Send("3"); return; }  // ping → pong
            if (text.StartsWith("0"))  { Log("[Tipestream] Engine.IO verbunden"); return; }
            if (text.StartsWith("40")) { JoinRoom(); return; }           // Socket.IO connect
            if (!text.StartsWith("42")) return;

            try
            {
                var json      = JArray.Parse(text[2..]);
                string evName = json[0].ToString();
                var data      = json.Count > 1 ? json[1] : null;

                switch (evName)
                {
                    case "connect-success":
                        Log("[Tipestream] Authentifiziert ✓");
                        break;
                    case "new-event":
                        Log($"[TS RAW] {data?.ToString(Newtonsoft.Json.Formatting.None)}");
                        HandleDonationEvent(data);
                        break;
                }
            }
            catch (Exception ex) { Log($"[Tipestream] Parse error: {ex.Message}"); }
        }

        private void JoinRoom()
        {
            var payload = new JArray("join-room",
                new JObject
                {
                    ["room"]     = _apiKey,
                    ["username"] = ""
                });
            _client?.Send("42" + payload.ToString());
            Log("[Tipestream] join-room sent");
        }

        private void HandleDonationEvent(JToken? data)
        {
            if (data == null) return;

            string? type = data["event"]?["type"]?.ToString();
            if (type != "donation") { Log($"[TS] Ignored: {type}"); return; }

            var parameters = data["event"]?["parameters"];
            if (parameters == null) return;

            decimal amount   = parameters["amount"]?.Value<decimal>()   ?? 0;
            string  donor    = parameters["username"]?.ToString()        ?? "Anonymous";
            string  message  = parameters["message"]?.ToString()         ?? string.Empty;
            string  currency = parameters["currency"]?.ToString()        ?? "EUR";

            Log($"[Tipestream] Donation: {amount} {currency} von {donor}");

            string? ytUrl = YouTubeHelper.FindYouTubeUrlInText(message);

            DonationReceived?.Invoke(new DonationEvent
            {
                DonorName  = donor,
                Amount     = amount,
                Currency   = currency,
                Message    = message,
                YouTubeUrl = ytUrl
            });
        }

        private void Log(string msg) => LogMessage?.Invoke(msg);

        public void Disconnect() =>
            _client?.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "disconnect");

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client?.Dispose();
        }
    }
}
