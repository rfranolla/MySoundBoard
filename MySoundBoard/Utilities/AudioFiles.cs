using System.IO;

namespace MySoundBoard.Utilities
{
    /// <summary>The audio formats the player can open, and helpers for picking them out of dropped paths.</summary>
    public static class AudioFiles
    {
        public static readonly IReadOnlyList<string> Extensions = new[] { ".mp3", ".wav", ".ogg" };

        public const string DialogFilter = "Audio files (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All files (*.*)|*.*";

        public static bool IsSupported(string path)
            => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Supported files from a mix of file and folder paths, in the order given.
        /// Folders contribute their top-level audio files, sorted by name.
        /// </summary>
        public static List<string> Expand(IEnumerable<string> paths)
        {
            var result = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    result.AddRange(Directory.EnumerateFiles(path)
                        .Where(IsSupported)
                        .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase));
                }
                else if (File.Exists(path) && IsSupported(path))
                {
                    result.Add(path);
                }
            }
            return result;
        }

        /// <summary>The closest folder above <paramref name="path"/> that still exists, for opening a file dialog there.</summary>
        public static string? NearestExistingDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                dir = Path.GetDirectoryName(dir);
            return string.IsNullOrEmpty(dir) ? null : dir;
        }
    }
}
