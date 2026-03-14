using DonationClipSystem.Models;

namespace DonationClipSystem.Services
{
    public class ClipQueueService : IDisposable
    {
        private readonly Queue<ClipItem> _queue = new();
        private readonly object _lock = new();
        private bool _isPlaying;
        private bool _disposed;
        private System.Threading.Timer? _finishTimer;

        public event Action<ClipItem>? ClipReady;
        public event Action?           ClipFinished;
        public event Action<string>?   LogMessage;

        private AppConfig _config;
        public ClipQueueService(AppConfig config) => _config = config;
        public void UpdateConfig(AppConfig config) => _config = config;
        public int  QueueCount { get { lock (_lock) return _queue.Count; } }

        public void Enqueue(DonationEvent donation)
        {
            var clip = BuildClip(donation);
            if (clip == null) { Log($"[Queue] Kein YouTube-Link gefunden für {donation.DonorName}"); return; }

            lock (_lock) { _queue.Enqueue(clip); }
            Log($"[Queue] Eingereiht für {donation.DonorName} (Queue: {_queue.Count})");
            TryPlayNext();
        }

        private ClipItem? BuildClip(DonationEvent donation)
        {
            if (string.IsNullOrWhiteSpace(donation.YouTubeUrl)) return null;

            string? videoId = YouTubeHelper.ExtractVideoId(donation.YouTubeUrl);
            if (videoId == null) return null;

            int start = YouTubeHelper.ExtractTimestampSeconds(donation.YouTubeUrl);
            Log($"[Queue] YouTube: {videoId}  Start={start}s");

            return new ClipItem
            {
                Type               = ClipType.YouTube,
                YouTubeVideoId     = videoId,
                StartSeconds       = start,
                MaxDurationSeconds = _config.MaxVideoLength,
                DonorName          = donation.DonorName,
                Amount             = donation.Amount,
                Currency           = donation.Currency
            };
        }

        private void TryPlayNext()
        {
            lock (_lock)
            {
                if (_isPlaying || _queue.Count == 0) return;
                _isPlaying = true;
                var clip = _queue.Dequeue();
                Log($"[Queue] Spiele: {clip.YouTubeVideoId} für {clip.DonorName}");
                int ms = (clip.MaxDurationSeconds + 2) * 1000;
                _finishTimer?.Dispose();
                _finishTimer = new System.Threading.Timer(_ => OnFinished(), null, ms, Timeout.Infinite);
                ClipReady?.Invoke(clip);
            }
        }

        private void OnFinished()
        {
            Log("[Queue] Clip beendet");
            lock (_lock) { _isPlaying = false; }
            ClipFinished?.Invoke();
            TryPlayNext();
        }

        public void NotifyClipEnded() { _finishTimer?.Dispose(); OnFinished(); }
        public void Skip()            { _finishTimer?.Dispose(); OnFinished(); }
        public void Clear()           { lock (_lock) { _queue.Clear(); } Log("[Queue] Geleert"); }
        private void Log(string m)    => LogMessage?.Invoke(m);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _finishTimer?.Dispose();
        }
    }
}
