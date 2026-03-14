using DonationClipSystem.Models;
using Newtonsoft.Json;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace DonationClipSystem.Services
{
    /// <summary>
    /// Reiner .NET HTTP + WebSocket Server — kein Fleck, kein COM-Konflikt.
    /// HTTP  → http://localhost:{httpPort}/overlay  (overlay.html + video files)
    /// WS    → ws://localhost:{httpPort}/ws         (play/stop events)
    /// </summary>
    public class OverlayServer : IDisposable
    {
        private readonly int _httpPort;
        private HttpListener? _listener;
        private readonly List<WebSocket> _clients = new();
        private readonly object _lock = new();
        private bool _disposed;
        private CancellationTokenSource _cts = new();

        public event Action<string>? LogMessage;

        public OverlayServer(int httpPort = 5000, int wsPort = 5000)
        {
            _httpPort = httpPort;
            // wsPort ignored — WS läuft auf demselben Port wie HTTP unter /ws
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_httpPort}/");
            _listener.Start();
            Log($"Overlay server: http://localhost:{_httpPort}/overlay  (WS: ws://localhost:{_httpPort}/ws)");
            Task.Run(AcceptLoop, _cts.Token);
        }

        private async Task AcceptLoop()
        {
            while (!_cts.Token.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleContext(ctx), _cts.Token);
                }
                catch { break; }
            }
        }

        private async Task HandleContext(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url?.AbsolutePath.ToLower().TrimStart('/') ?? "";

            // WebSocket upgrade
            if (ctx.Request.IsWebSocketRequest && path == "ws")
            {
                var wsCtx = await ctx.AcceptWebSocketAsync(null);
                var ws    = wsCtx.WebSocket;
                lock (_lock) _clients.Add(ws);
                Log("[WS] Overlay verbunden");
                await WsReceiveLoop(ws);
                lock (_lock) _clients.Remove(ws);
                Log("[WS] Overlay getrennt");
                return;
            }

            // HTTP routes
            if (path == "overlay" || path == "" || path == "index")
            { ServeFile(ctx, "overlay.html", "text/html; charset=utf-8"); return; }

            if (path == "player")
            { ServeFile(ctx, "player.html", "text/html; charset=utf-8"); return; }

            if (path == "video")
            { ServeVideo(ctx); return; }

            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }

        private static async Task WsReceiveLoop(WebSocket ws)
        {
            var buf = new byte[256];
            try
            {
                while (ws.State == WebSocketState.Open)
                    await ws.ReceiveAsync(buf, CancellationToken.None);
            }
            catch { }
        }

        private static void ServeFile(HttpListenerContext ctx, string fileName, string mime)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", fileName);
            if (!File.Exists(path)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }
            byte[] data = File.ReadAllBytes(path);
            ctx.Response.ContentType     = mime;
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data);
            ctx.Response.Close();
        }

        private static void ServeVideo(HttpListenerContext ctx)
        {
            string? enc = ctx.Request.QueryString["path"];
            if (string.IsNullOrEmpty(enc)) { ctx.Response.StatusCode = 400; ctx.Response.Close(); return; }
            string file = Uri.UnescapeDataString(enc);
            if (!File.Exists(file)) { ctx.Response.StatusCode = 404; ctx.Response.Close(); return; }

            string mime = Path.GetExtension(file).ToLower() switch
            {
                ".mp4"  => "video/mp4",
                ".webm" => "video/webm",
                _       => "application/octet-stream"
            };
            long size = new FileInfo(file).Length;
            string? range = ctx.Request.Headers["Range"];
            using var fs = File.OpenRead(file);

            if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes="))
            {
                var parts = range[6..].Split('-');
                long start = long.Parse(parts[0]);
                long end   = parts[1].Length > 0 ? long.Parse(parts[1]) : size - 1;
                long len   = end - start + 1;
                ctx.Response.StatusCode  = 206;
                ctx.Response.ContentType = mime;
                ctx.Response.ContentLength64 = len;
                ctx.Response.AddHeader("Content-Range",  $"bytes {start}-{end}/{size}");
                ctx.Response.AddHeader("Accept-Ranges",  "bytes");
                fs.Seek(start, SeekOrigin.Begin);
                var buf = new byte[65536];
                long rem = len;
                while (rem > 0)
                {
                    int r = fs.Read(buf, 0, (int)Math.Min(buf.Length, rem));
                    if (r == 0) break;
                    ctx.Response.OutputStream.Write(buf, 0, r);
                    rem -= r;
                }
            }
            else
            {
                ctx.Response.ContentType = mime;
                ctx.Response.ContentLength64 = size;
                fs.CopyTo(ctx.Response.OutputStream);
            }
            ctx.Response.Close();
        }

        public void SendPlay(ClipItem clip)
        {
            var msg = JsonConvert.SerializeObject(new
            {
                action    = "play",
                clipType  = clip.Type.ToString().ToLower(),
                url       = clip.Type == ClipType.YouTube
                                ? clip.GetYouTubeEmbedUrl()
                                : clip.GetLocalFileOverlayUrl(_httpPort),
                donorName = clip.DonorName,
                amount    = clip.Amount,
                currency  = clip.Currency,
                duration  = clip.MaxDurationSeconds
            });
            Broadcast(msg);
        }

        public void SendStop() => Broadcast(JsonConvert.SerializeObject(new { action = "stop" }));

        private void Broadcast(string json)
        {
            var data = Encoding.UTF8.GetBytes(json);
            List<WebSocket> snap;
            lock (_lock) snap = new List<WebSocket>(_clients);
            foreach (var ws in snap)
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        ws.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None)
                          .GetAwaiter().GetResult();
                }
                catch { }
            }
        }

        private void Log(string msg) => LogMessage?.Invoke(msg);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _listener?.Stop();
        }
    }
}