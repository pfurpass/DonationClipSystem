namespace DonationClipSystem
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelSettings;
        private Panel panelPlayer;
        private Panel panelLog;

        private Label lblPlatform;
        private ComboBox comboPlatform;
        private Label lblToken;
        private TextBox txtToken;
        private CheckBox chkSaveToken;
        private Label lblMinDonation;
        private NumericUpDown numMinDonation;
        private Label lblMaxLength;
        private NumericUpDown numMaxLength;

        private Button btnConnect;
        private Button btnTest;
        private Button btnSave;
        private Button btnSkip;
        private Button btnClear;
        private Button btnCopyOverlay;

        private Label lblPlayerTitle;
        private Label lblNowPlaying;
        private Label lblQueueCount;
        private Label lblOverlayUrl;
        private RichTextBox richTextBoxLog;
        private ToolTip toolTip;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            toolTip = new ToolTip(components);

            var bg = Color.FromArgb(28, 28, 30);
            var panel = Color.FromArgb(38, 38, 40);
            var input = Color.FromArgb(52, 52, 55);
            var silver = Color.FromArgb(190, 190, 195);
            var green = Color.FromArgb(0, 120, 60);

            // ── Settings ─────────────────────────────────────────────────────
            panelSettings = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(320, 600),
                BackColor = panel
            };

            int y = 16; int lx = 14, cx = 130, cw = 178;

            AddLabel("⚙  Settings", lx, y, 290, 22, true, 11f); y += 34;

            lblPlatform = AddLabel("Platform:", lx, y, 110, 22);
            comboPlatform = new ComboBox
            {
                Location = new Point(cx, y),
                Size = new Size(cw, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = input,
                ForeColor = Color.White
            };
            comboPlatform.Items.AddRange(new object[] { "StreamElements", "Tipestream" });
            comboPlatform.SelectedIndex = 0;
            comboPlatform.SelectedIndexChanged += comboPlatform_SelectedIndexChanged;
            panelSettings.Controls.Add(comboPlatform);
            y += 32;

            lblToken = AddLabel("JWT Token:", lx, y, 110, 22);
            txtToken = new TextBox
            {
                Location = new Point(cx, y),
                Size = new Size(cw, 26),
                PasswordChar = '●',
                BackColor = input,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelSettings.Controls.Add(txtToken);
            y += 32;

            chkSaveToken = new CheckBox
            {
                Text = "Save token to config.json",
                Location = new Point(cx, y),
                Size = new Size(cw, 20),
                ForeColor = silver,
                Checked = true
            };
            panelSettings.Controls.Add(chkSaveToken);
            y += 32;

            panelSettings.Controls.Add(MakeSep(lx, y, 292)); y += 16;

            lblMinDonation = AddLabel("Min. Donation (€):", lx, y, 118, 22);
            numMinDonation = new NumericUpDown
            {
                Location = new Point(cx, y),
                Size = new Size(90, 26),
                Minimum = 0,
                Maximum = 10000,
                DecimalPlaces = 2,
                Value = 5,
                BackColor = input,
                ForeColor = Color.White
            };
            panelSettings.Controls.Add(numMinDonation);
            y += 32;

            lblMaxLength = AddLabel("Max Clip Length (s):", lx, y, 118, 22);
            numMaxLength = new NumericUpDown
            {
                Location = new Point(cx, y),
                Size = new Size(90, 26),
                Minimum = 5,
                Maximum = 600,
                Value = 30,
                BackColor = input,
                ForeColor = Color.White
            };
            panelSettings.Controls.Add(numMaxLength);
            y += 32;

            panelSettings.Controls.Add(MakeSep(lx, y, 292)); y += 16;

            panelSettings.Controls.Add(new Label
            {
                Text = "Donors include a YouTube link\nin their donation message → detected\nand played automatically.\n\nTimestamps supported: youtu.be/ID?t=90",
                Location = new Point(lx, y),
                Size = new Size(292, 90),
                ForeColor = Color.FromArgb(130, 130, 140),
                Font = new Font("Segoe UI", 8.5f)
            });
            y += 98;

            panelSettings.Controls.Add(MakeSep(lx, y, 292)); y += 16;

            lblOverlayUrl = new Label
            {
                Text = "OBS URL: http://localhost:5000/overlay",
                Location = new Point(lx, y),
                Size = new Size(210, 36),
                ForeColor = Color.FromArgb(90, 160, 255),
                Font = new Font("Segoe UI", 7.5f),
                AutoSize = false
            };
            panelSettings.Controls.Add(lblOverlayUrl);

            btnCopyOverlay = MakeBtn("📋 Copy", lx + 214, y + 4, 94, 26);
            btnCopyOverlay.Click += btnCopyOverlay_Click;
            y += 48;

            panelSettings.Controls.Add(MakeSep(lx, y, 292)); y += 16;

            btnSave = MakeBtn("💾 Save", lx, y, 140, 34);
            btnConnect = MakeBtn("▶ Connect", lx + 148, y, 160, 34);
            btnConnect.BackColor = green;
            btnSave.Click += btnSave_Click;
            btnConnect.Click += btnConnect_Click;
            y += 42;

            btnTest = MakeBtn("🎬 Test Clip", lx, y, 140, 34);
            btnSkip = MakeBtn("⏭ Skip", lx + 148, y, 72, 34);
            btnClear = MakeBtn("🗑 Clear", lx + 228, y, 80, 34);
            btnTest.Click += btnTest_Click;
            btnSkip.Click += btnSkip_Click;
            btnClear.Click += btnClear_Click;

            panelSettings.Controls.AddRange(new Control[] { btnSave, btnConnect, btnTest, btnSkip, btnClear });

            // ── Player ───────────────────────────────────────────────────────
            var outerPlayer = new Panel
            {
                Location = new Point(340, 10),
                Size = new Size(558, 400),
                BackColor = Color.FromArgb(18, 18, 20)
            };

            lblPlayerTitle = new Label
            {
                Text = "🎥  Preview Player (Streamer Only)",
                Location = new Point(8, 6),
                Size = new Size(540, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Parent = outerPlayer
            };

            panelPlayer = new Panel
            {
                Location = new Point(0, 28),
                Size = new Size(558, 346),
                BackColor = Color.Black
            };
            outerPlayer.Controls.Add(panelPlayer);

            lblNowPlaying = new Label
            {
                Text = "Now Playing: —",
                Location = new Point(8, 376),
                Size = new Size(430, 20),
                ForeColor = silver,
                Font = new Font("Segoe UI", 8f),
                Parent = outerPlayer
            };
            lblQueueCount = new Label
            {
                Text = "Queue: 0",
                Location = new Point(450, 376),
                Size = new Size(100, 20),
                ForeColor = Color.FromArgb(255, 200, 0),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Parent = outerPlayer
            };

            // ── Log ──────────────────────────────────────────────────────────
            panelLog = new Panel
            {
                Location = new Point(340, 418),
                Size = new Size(558, 192),
                BackColor = Color.FromArgb(18, 18, 20)
            };
            new Label
            {
                Text = "📋  Event Log",
                Location = new Point(6, 4),
                Size = new Size(540, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Parent = panelLog
            };
            richTextBoxLog = new RichTextBox
            {
                Location = new Point(4, 24),
                Size = new Size(548, 162),
                BackColor = Color.FromArgb(18, 18, 20),
                ForeColor = Color.FromArgb(80, 220, 100),
                Font = new Font("Consolas", 8f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Parent = panelLog
            };

            // ── Form ─────────────────────────────────────────────────────────
            Text = "DonationClipSystem v1.0";
            Size = new Size(920, 660);
            MinimumSize = new Size(920, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            Load += MainForm_Load;   // ← Load-Event registrieren

            Controls.AddRange(new Control[] { panelSettings, outerPlayer, panelLog });
        }

        private Label AddLabel(string text, int x, int y, int w, int h, bool bold = false, float size = 9f)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = bold ? Color.White : Color.FromArgb(190, 190, 195),
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
            };
            panelSettings.Controls.Add(lbl);
            return lbl;
        }

        private Button MakeBtn(string text, int x, int y, int w, int h)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 58, 62),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f)
            };
            panelSettings.Controls.Add(btn);
            return btn;
        }

        private static Label MakeSep(int x, int y, int w) =>
            new()
            {
                Location = new Point(x, y),
                Size = new Size(w, 1),
                BackColor = Color.FromArgb(60, 60, 65),
                Text = ""
            };
    }
}