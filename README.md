# DonationClipSystem

A C# WinForms application for streamers that monitors donations from **StreamElements** or **Tipestream** and automatically plays video clips — both in OBS (via a browser source overlay) and in a local streamer preview window.

---

## Requirements

| Component | Version |
|---|---|
| .NET SDK | 6.0 or later |
| Windows | 10 / 11 |
| VLC | Bundled via NuGet (VideoLAN.LibVLC.Windows) |
| OBS Studio | Any recent version |

---

## Setup

### 1. Build

```bash
cd DonationClipSystem
dotnet restore
dotnet build -c Release
```

Or open `DonationClipSystem.csproj` in Visual Studio 2022.

### 2. Run

```
dotnet run
# or double-click the Release .exe
```

### 3. Configure

Fill in the **Settings** panel on the left:

| Setting | Description |
|---|---|
| Platform | StreamElements or Tipestream |
| API Token | StreamElements JWT token **or** Tipestream API key |
| Save Token | Tick to persist token in `config.json` |
| Min. Donation | Minimum amount that triggers a clip |
| Max Clip Length | How many seconds of the clip to play (max) |
| Clip Source | Local folder, YouTube link, or random from folder |

### 4. Add OBS Browser Source

1. In OBS, add a **Browser Source**.
2. Set URL to: `http://localhost:5000/overlay`
3. Set width/height to your stream resolution (e.g. 1920×1080).
4. Tick **Refresh browser when scene becomes active**.

> **Tip:** The overlay page background is transparent, so it works as an overlay on top of your normal scene.

---

## Getting API Tokens

### StreamElements
1. Log in at [streamelements.com](https://streamelements.com)
2. Go to **Account → Show Secrets**
3. Copy your **JWT Token**

### Tipestream
1. Log in at [tipeeestream.com](https://tipeeestream.com)
2. Go to your profile → **API Key**
3. Copy your API key

---

## YouTube URL Support

Paste any YouTube URL into the **YouTube Link** field, or have donors include a link in their donation message. The app recognises all standard formats:

| Format | Start time |
|---|---|
| `https://youtu.be/abc123` | 0s |
| `https://youtu.be/abc123?t=45` | 45s |
| `https://youtu.be/abc123?t=1m30s` | 90s |
| `https://youtube.com/watch?v=abc123&t=1h2m3s` | 3723s |

The clip plays from the timestamp up to `timestamp + Max Clip Length`.

---

## Local Video Folder

Place `.mp4`, `.webm`, `.mov`, `.avi`, or `.mkv` files in a folder and point the app to it. A random file is chosen for each donation.

---

## Donation Queue

Multiple donations are queued and played back-to-back. Use **⏭ Skip** to skip the current clip, or **🗑 Clear** to empty the queue.

---

## Architecture

```
DonationClipSystem (WinForms)
│
├── OverlayServer
│   ├── HTTP  → http://localhost:5000/overlay  (serves overlay.html + video files)
│   └── WS    → ws://localhost:5001             (pushes play/stop events)
│
├── StreamElementsService   ← WebSocket to StreamElements realtime API
├── TipestreamService       ← WebSocket to Tipestream Socket.IO API
│
├── ClipQueueService        ← Manages clip queue and playback timing
│
└── MainForm
    ├── VLC VideoView       ← Local preview for local files
    └── WebBrowser          ← Local preview for YouTube embeds
```

---

## config.json

Saved automatically in the application folder:

```json
{
  "platform": "StreamElements",
  "token": "YOUR_JWT_HERE",
  "saveToken": true,
  "minDonation": 5.0,
  "maxVideoLength": 30,
  "clipSource": "RandomFromFolder",
  "clipFolder": "C:\\DonationClips",
  "youtubeLink": "",
  "overlayPort": 5000,
  "wsPort": 5001,
  "showDonorName": true
}
```

---

## NuGet Packages

| Package | Purpose |
|---|---|
| `LibVLCSharp` + `LibVLCSharp.WinForms` | Local video playback |
| `VideoLAN.LibVLC.Windows` | VLC native binaries |
| `Newtonsoft.Json` | JSON config serialization |
| `Websocket.Client` | Connect to StreamElements/Tipestream WS APIs |
| `Fleck` | Serve WebSocket connections to the overlay |

---

## Troubleshooting

**Overlay is blank in OBS**
- Make sure the app is running before OBS loads the browser source.
- Check that port 5000 is not blocked by a firewall.

**VLC fails to initialize**
- Ensure the `VideoLAN.LibVLC.Windows` NuGet package is installed — it bundles the native VLC libs.
- The app will fall back to YouTube-only mode if VLC fails.

**StreamElements not receiving events**
- Double-check your JWT token (not your password).
- Look at the Event Log panel for error messages.

**Tipestream not connecting**
- Use your **API Key**, not your login credentials.
- The Tipestream Socket.IO endpoint sometimes changes; check their developer docs if reconnection loops.
