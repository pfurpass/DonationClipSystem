namespace DonationClipSystem.Models
{
    public class DonationEvent
    {
        public string DonorName { get; set; } = "Anonymous";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string Message { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// If the donation message contains a YouTube link, it is stored here.
        /// </summary>
        public string? YouTubeUrl { get; set; }
    }
}
