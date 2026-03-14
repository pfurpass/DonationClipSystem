using Newtonsoft.Json;

namespace DonationClipSystem.Models
{
    public enum StreamPlatform { StreamElements, Tipestream }

    public class AppConfig
    {
        [JsonProperty("platform")]
        public StreamPlatform Platform { get; set; } = StreamPlatform.StreamElements;

        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("saveToken")]
        public bool SaveToken { get; set; } = true;

        [JsonProperty("minDonation")]
        public decimal MinDonation { get; set; } = 5m;

        [JsonProperty("maxVideoLength")]
        public int MaxVideoLength { get; set; } = 30;

        [JsonProperty("overlayPort")]
        public int OverlayPort { get; set; } = 5000;

        [JsonProperty("wsPort")]
        public int WebSocketPort { get; set; } = 5001;
    }
}
