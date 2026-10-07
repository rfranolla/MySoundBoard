using MySoundBoard.Utilities;
using System.IO;
using System.Text.Json;

namespace MySoundBoard.Managers
{
    public class AppSettings
    {
        public string PrimaryDeviceName { get; set; } = string.Empty;
        public string HeadphoneDeviceName { get; set; } = string.Empty;
        public double GlobalVolume { get; set; } = 100;

        /// <summary>"Light" or "Dark".</summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>Last normal (un-maximized) window bounds; null until the window has been closed once.</summary>
        public WindowBounds? Window { get; set; }

        /// <summary>Board reopened at startup; empty when the last session ended on an unsaved board.</summary>
        public string LastBoardPath { get; set; } = string.Empty;

        public HotkeySetting? StopAllHotkey { get; set; }

        private static string SettingsPath => AppPaths.SettingsFile;

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
                // Same temp-then-rename as board saves, so a crash mid-write can't leave a truncated file.
                var tmp = SettingsPath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this));
                File.Move(tmp, SettingsPath, overwrite: true);
            }
            catch { }
        }
    }

    public class WindowBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }
    }

    public class HotkeySetting
    {
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
        public string Display { get; set; } = string.Empty;
    }
}
