using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Utilities;

namespace MySoundBoard.Tests
{
    [TestClass]
    public static class TestAssemblySetup
    {
        private static string? _dataDirectory;

        // Redirect settings and boards to a throwaway folder so tests never read, overwrite or
        // delete the real %APPDATA%\MySoundBoard data of whoever runs them.
        [AssemblyInitialize]
        public static void Initialize(TestContext _)
        {
            _dataDirectory = Path.Combine(Path.GetTempPath(), "MySoundBoard.Tests", Guid.NewGuid().ToString("N"));
            AppPaths.DataDirectory = _dataDirectory;
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            try
            {
                if (_dataDirectory != null && Directory.Exists(_dataDirectory))
                    Directory.Delete(_dataDirectory, recursive: true);
            }
            catch (IOException) { }
        }
    }
}
