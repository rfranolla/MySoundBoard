using System.Text.RegularExpressions;

namespace MySoundBoard.Utilities
{
    public static class IconNameFormatter
    {
        public static string FormatDisplayName(string rawEnumName)
        {
            var name = rawEnumName.EndsWith("48") ? rawEnumName[..^2] : rawEnumName;
            // "TVMonitor" → "TV Monitor" (acronym before a title-case word)
            name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1 $2");
            // "ChevronLeft" → "Chevron Left" (lowercase → uppercase boundary)
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            // "MusicNote1" → "Music Note 1" (letter → digit boundary)
            name = Regex.Replace(name, "([a-zA-Z])(\\d)", "$1 $2");
            return name.Trim();
        }
    }
}
