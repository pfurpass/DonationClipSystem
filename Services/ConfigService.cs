using DonationClipSystem.Models;
using Newtonsoft.Json;

namespace DonationClipSystem.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
            }
            catch { }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var toSave = config.SaveToken ? config : new AppConfig
                {
                    Platform       = config.Platform,
                    SaveToken      = false,
                    MinDonation    = config.MinDonation,
                    MaxVideoLength = config.MaxVideoLength,
                    OverlayPort    = config.OverlayPort,
                    WebSocketPort  = config.WebSocketPort
                };
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(toSave, Formatting.Indented));
            }
            catch { }
        }
    }
}
