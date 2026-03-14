using DonationClipSystem.Models;
using DonationClipSystem.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DonationClipSystem
{
    public partial class MainForm : Form
    {
        private AppConfig _config = new();
        private OverlayServer?         _overlayServer;
        private ClipQueueService?      _queueService;
        private StreamElementsService? _seService;
        private TipestreamService?     _tsService;
        private WebView2               _webView = null!;
        private bool                   _webViewReady;
        private bool                   _isConnected;

        public MainForm()
        {
            InitializeComponent();
            LoadConfig();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitWebViewAsync();
            SetupOverlayServer();
            SetupQueueService();
        }

        private async Task InitWebViewAsync()
        {
            try
            {
                // WebView2 User-Data-Folder explizit setzen
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DonationClipSystem", "WebView2");

                // Autoplay ohne User-Geste erlauben
                var opts = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
                };
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, opts);

                _webView = new WebView2
                {
                    Dock             = DockStyle.Fill,
                    BackColor        = Color.Black,
                    DefaultBackgroundColor = Color.Black
                };
                panelPlayer.Controls.Add(_webView);
                await _webView.EnsureCoreWebView2Async(env);

                // Chrome User-Agent setzen → verhindert YouTube Fehler 153
                _webView.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/124.0.0.0 Safari/537.36";

                _webView.CoreWebView2.Settings.IsScriptEnabled               = true;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled            = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled          = false;
                _webView.CoreWebView2.Navigate("about:blank");

                _webViewReady = true;
                LogAppend("[WebView2] ✓ Bereit");
            }
            catch (Exception ex)
            {
                LogAppend($"[WebView2] FEHLER: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            _config = ConfigService.Load();
            ApplyConfigToUI();
        }

        private void SetupOverlayServer()
        {
            _overlayServer = new OverlayServer(_config.OverlayPort);
            _overlayServer.LogMessage += msg => LogAppend(msg);
            _overlayServer.Start();
            lblOverlayUrl.Text = $"OBS URL: http://localhost:{_config.OverlayPort}/overlay";
        }

        private void SetupQueueService()
        {
            _queueService?.Dispose();
            _queueService = new ClipQueueService(_config);
            _queueService.LogMessage   += msg  => LogAppend(msg);
            _queueService.ClipReady    += clip => BeginInvoke(() => PlayClip(clip));
            _queueService.ClipFinished += ()   => BeginInvoke(StopPlayer);
        }

        private void ApplyConfigToUI()
        {
            comboPlatform.SelectedIndex = _config.Platform == StreamPlatform.StreamElements ? 0 : 1;
            txtToken.Text               = _config.SaveToken ? _config.Token : string.Empty;
            chkSaveToken.Checked        = _config.SaveToken;
            numMinDonation.Value        = Math.Max(0, _config.MinDonation);
            numMaxLength.Value          = Math.Max(5, Math.Min(600, _config.MaxVideoLength));
        }

        private void SaveConfigFromUI()
        {
            _config.Platform       = comboPlatform.SelectedIndex == 0 ? StreamPlatform.StreamElements : StreamPlatform.Tipestream;
            _config.Token          = txtToken.Text.Trim();
            _config.SaveToken      = chkSaveToken.Checked;
            _config.MinDonation    = numMinDonation.Value;
            _config.MaxVideoLength = (int)numMaxLength.Value;
            _queueService?.UpdateConfig(_config);
            ConfigService.Save(_config);
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_isConnected) { Disconnect(); return; }
            SaveConfigFromUI();
            if (string.IsNullOrWhiteSpace(_config.Token))
            {
                MessageBox.Show("Bitte API Token eingeben.", "Fehlt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnConnect.Enabled = false;
            btnConnect.Text    = "Verbinde…";
            try
            {
                if (_config.Platform == StreamPlatform.StreamElements)
                {
                    _seService?.Dispose();
                    _seService = new StreamElementsService(_config.Token);
                    _seService.LogMessage       += msg => LogAppend(msg);
                    _seService.DonationReceived += HandleDonation;
                    await _seService.ConnectAsync();
                }
                else
                {
                    _tsService?.Dispose();
                    _tsService = new TipestreamService(_config.Token);
                    _tsService.LogMessage       += msg => LogAppend(msg);
                    _tsService.DonationReceived += HandleDonation;
                    await _tsService.ConnectAsync();
                }
                _isConnected         = true;
                btnConnect.Text      = "⬛ Trennen";
                btnConnect.BackColor = Color.FromArgb(160, 30, 30);
            }
            catch (Exception ex)
            {
                LogAppend($"[FEHLER] {ex.Message}");
                btnConnect.Text      = "▶ Verbinden";
                btnConnect.BackColor = Color.FromArgb(0, 120, 60);
                MessageBox.Show(ex.Message, "Verbindungsfehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { btnConnect.Enabled = true; }
        }

        private void Disconnect()
        {
            _seService?.Disconnect();
            _tsService?.Disconnect();
            _isConnected         = false;
            btnConnect.Text      = "▶ Verbinden";
            btnConnect.BackColor = Color.FromArgb(0, 120, 60);
            LogAppend("Getrennt.");
        }

        private void HandleDonation(DonationEvent donation)
        {
            BeginInvoke(() =>
            {
                LogAppend($"💰 {donation.Amount} {donation.Currency} von {donation.DonorName}");
                if (!string.IsNullOrEmpty(donation.Message))
                    LogAppend($"   Msg: {donation.Message}");

                if (donation.Amount < _config.MinDonation)
                {
                    LogAppend($"   Ignoriert (unter Minimum {_config.MinDonation})");
                    return;
                }
                if (string.IsNullOrEmpty(donation.YouTubeUrl))
                {
                    LogAppend("   Kein YouTube-Link in Nachricht");
                    return;
                }
                lblQueueCount.Text = $"Queue: {(_queueService?.QueueCount ?? 0) + 1}";
                _queueService!.Enqueue(donation);
            });
        }

        private ClipItem? _currentClip;

        private void PlayClip(ClipItem clip)
        {
            if (!_webViewReady) { LogAppend("WebView2 nicht bereit!"); return; }
            _currentClip = clip;
            _overlayServer?.SendPlay(clip);

            // Lokale player.html über localhost laden
            string playerUrl = $"http://localhost:{_config.OverlayPort}/player" +
                               $"?v={clip.YouTubeVideoId}&t={clip.StartSeconds}&len={clip.MaxDurationSeconds}";
            _webView.CoreWebView2.Navigate(playerUrl);

            LogAppend($"▶ {clip.DonorName} | YT/{clip.YouTubeVideoId} ab {clip.StartSeconds}s");
            lblNowPlaying.Text = $"▶  {clip.YouTubeVideoId}  [{clip.DonorName} {clip.Amount}{clip.Currency}]";
            lblQueueCount.Text = $"Queue: {_queueService?.QueueCount ?? 0}";
        }

        private void StopPlayer()
        {
            _overlayServer?.SendStop();
            if (_webViewReady) _webView.CoreWebView2.Navigate("about:blank");
            lblNowPlaying.Text = "Now Playing: —";
            lblQueueCount.Text = $"Queue: {_queueService?.QueueCount ?? 0}";
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            SaveConfigFromUI();
            if (!_webViewReady) { LogAppend("WebView2 noch nicht bereit!"); return; }
            HandleDonation(new DonationEvent
            {
                DonorName  = "TestUser",
                Amount     = _config.MinDonation,
                Currency   = "EUR",
                Message    = "https://youtu.be/dQw4w9WgXcQ?t=10",
                YouTubeUrl = "https://youtu.be/dQw4w9WgXcQ?t=10"
            });
        }

        private void btnSkip_Click(object sender, EventArgs e)  => _queueService?.Skip();
        private void btnClear_Click(object sender, EventArgs e) { _queueService?.Clear(); lblQueueCount.Text = "Queue: 0"; }
        private void btnSave_Click(object sender, EventArgs e)  { SaveConfigFromUI(); LogAppend("Gespeichert."); }

        private void btnCopyOverlay_Click(object sender, EventArgs e)
        {
            Clipboard.SetText($"http://localhost:{_config.OverlayPort}/overlay");
            toolTip.Show("Kopiert!", btnCopyOverlay, 0, -20, 1500);
        }

        private void comboPlatform_SelectedIndexChanged(object sender, EventArgs e)
            => lblToken.Text = comboPlatform.SelectedIndex == 0 ? "JWT Token:" : "API Key:";

        private void LogAppend(string msg)
        {
            if (InvokeRequired) { BeginInvoke(() => LogAppend(msg)); return; }
            richTextBoxLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            richTextBoxLog.ScrollToCaret();
            if (richTextBoxLog.Lines.Length > 600)
            {
                richTextBoxLog.Select(0, richTextBoxLog.GetFirstCharIndexFromLine(100));
                richTextBoxLog.SelectedText = string.Empty;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveConfigFromUI();
            _queueService?.Dispose();
            _overlayServer?.Dispose();
            _seService?.Dispose();
            _tsService?.Dispose();
        }
    }
}

namespace DonationClipSystem
{
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
    }
}