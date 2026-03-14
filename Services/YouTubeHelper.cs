using System.Text.RegularExpressions;

namespace DonationClipSystem.Services
{
    public static class YouTubeHelper
    {
        // Matches standard, short, and embed YouTube URLs
        private static readonly Regex VideoIdRegex = new(
            @"(?:youtube\.com\/(?:watch\?(?:.*&)?v=|embed\/|shorts\/)|youtu\.be\/)([A-Za-z0-9_\-]{11})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Matches timestamp formats: ?t=45, &t=45, ?t=1m30s, ?t=1h2m3s
        private static readonly Regex TimestampRegex = new(
            @"[?&]t=(?:(?:(\d+)h)?(?:(\d+)m)?(\d+)s?|(\d+))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Extracts the YouTube video ID from a URL. Returns null if not found.
        /// </summary>
        public static string? ExtractVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var match = VideoIdRegex.Match(url);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Parses the timestamp from a YouTube URL and returns it in seconds.
        /// Returns 0 if no timestamp is found.
        /// </summary>
        public static int ExtractTimestampSeconds(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return 0;

            var match = TimestampRegex.Match(url);
            if (!match.Success) return 0;

            // Case: plain seconds only  e.g. ?t=90
            if (match.Groups[4].Success)
                return int.Parse(match.Groups[4].Value);

            // Case: h/m/s components  e.g. ?t=1h2m30s
            int h = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            int m = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int s = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            return h * 3600 + m * 60 + s;
        }

        /// <summary>
        /// Checks whether a string looks like a YouTube URL.
        /// </summary>
        public static bool IsYouTubeUrl(string text)
            => !string.IsNullOrWhiteSpace(text) && VideoIdRegex.IsMatch(text);

        /// <summary>
        /// Tries to extract the first YouTube URL found inside a longer message string.
        /// </summary>
        public static string? FindYouTubeUrlInText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Split by whitespace and check each token
            foreach (var token in text.Split(' ', '\n', '\r', '\t'))
            {
                if (IsYouTubeUrl(token)) return token.Trim();
            }
            return null;
        }
    }
}
