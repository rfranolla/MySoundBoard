using System.IO;

namespace MySoundBoard.Utilities
{
    /// <summary>Where the app keeps its settings and boards.</summary>
    public static class AppPaths
    {
        private static string _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MySoundBoard");

        /// <summary>Root data folder. Tests point this at a temp folder so they never touch real user data.</summary>
        public static string DataDirectory
        {
            get => _dataDirectory;
            internal set => _dataDirectory = value;
        }

        public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
        public static string BoardsDirectory => Path.Combine(DataDirectory, "SoundBoards");
    }
}
