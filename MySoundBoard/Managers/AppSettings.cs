using System.IO;
using System.Text.Json;

namespace MySoundBoard.Managers
{
    public class AppSettings
    {
        public string PrimaryDeviceName { get; set; } = string.Empty;
        public string HeadphoneDeviceName { get; set; } = string.Empty;
        public double GlobalVolume { get; set; } = 100;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MySoundBoard", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
            }
            catch { }
        }
    }
}
