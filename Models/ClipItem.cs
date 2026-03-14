namespace DonationClipSystem.Models
{
    public enum ClipType { LocalFile, YouTube }

    public class ClipItem
    {
        public ClipType Type { get; set; }

        /// <summary>Full path for local files.</summary>
        public string? FilePath { get; set; }

        /// <summary>YouTube video ID (11 chars).</summary>
        public string? YouTubeVideoId { get; set; }

        /// <summary>Start time in seconds (from timestamp parsing or 0).</summary>
        public int StartSeconds { get; set; } = 0;

        /// <summary>Max duration in seconds.</summary>
        public int MaxDurationSeconds { get; set; } = 30;

        /// <summary>Donor name for overlay display.</summary>
        public string DonorName { get; set; } = string.Empty;

        /// <summary>Donation amount for overlay display.</summary>
        public decimal Amount { get; set; }

        /// <summary>Currency symbol.</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>Returns the YouTube embed URL with autoplay, start time and end time.</summary>
        public string GetYouTubeEmbedUrl()
        {
            if (string.IsNullOrEmpty(YouTubeVideoId))
                return string.Empty;

            int end = StartSeconds + MaxDurationSeconds;
            return $"https://www.youtube-nocookie.com/embed/{YouTubeVideoId}" +
                   $"?autoplay=1&start={StartSeconds}&end={end}" +
                   $"&controls=0&modestbranding=1&rel=0&enablejsapi=1&origin=http://localhost:5000";
        }

        /// <summary>Returns a local HTTP URL to serve the file via the overlay server.</summary>
        public string GetLocalFileOverlayUrl(int port)
        {
            if (string.IsNullOrEmpty(FilePath))
                return string.Empty;

            string encoded = Uri.EscapeDataString(FilePath);
            return $"http://localhost:{port}/video?path={encoded}&start={StartSeconds}&maxLen={MaxDurationSeconds}";
        }
    }
}
