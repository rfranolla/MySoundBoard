using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Managers;

namespace MySoundBoard.Tests.Managers
{
    [TestClass]
    public class AppSettingsTests
    {
        private static string GetSettingsPath()
        {
            return (string)typeof(AppSettings)
                .GetProperty("SettingsPath", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Restore a clean state so tests don't interfere with each other
            var path = GetSettingsPath();
            if (File.Exists(path)) File.Delete(path);
        }

        // ── Default values ────────────────────────────────────────────────────

        [TestMethod]
        public void DefaultValues_GlobalVolumeIs100()
        {
            var settings = new AppSettings();
            Assert.AreEqual(100.0, settings.GlobalVolume);
        }

        [TestMethod]
        public void DefaultValues_PrimaryDeviceNameIsEmpty()
        {
            var settings = new AppSettings();
            Assert.AreEqual(string.Empty, settings.PrimaryDeviceName);
        }

        [TestMethod]
        public void DefaultValues_HeadphoneDeviceNameIsEmpty()
        {
            var settings = new AppSettings();
            Assert.AreEqual(string.Empty, settings.HeadphoneDeviceName);
        }

        // ── Property assignment ───────────────────────────────────────────────

        [TestMethod]
        public void Properties_RetainAssignedValues()
        {
            var settings = new AppSettings
            {
                PrimaryDeviceName = "Speakers (Realtek)",
                HeadphoneDeviceName = "CABLE Input",
                GlobalVolume = 65.0
            };

            Assert.AreEqual("Speakers (Realtek)", settings.PrimaryDeviceName);
            Assert.AreEqual("CABLE Input", settings.HeadphoneDeviceName);
            Assert.AreEqual(65.0, settings.GlobalVolume);
        }

        // ── JSON serialization ────────────────────────────────────────────────

        [TestMethod]
        public void JsonRoundtrip_PreservesAllProperties()
        {
            var original = new AppSettings
            {
                PrimaryDeviceName = "Device A",
                HeadphoneDeviceName = "Device B",
                GlobalVolume = 42.5
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;

            Assert.AreEqual(original.PrimaryDeviceName, deserialized.PrimaryDeviceName);
            Assert.AreEqual(original.HeadphoneDeviceName, deserialized.HeadphoneDeviceName);
            Assert.AreEqual(original.GlobalVolume, deserialized.GlobalVolume);
        }

        [TestMethod]
        public void JsonRoundtrip_WithDefaultValues_PreservesDefaults()
        {
            var original = new AppSettings();
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;

            Assert.AreEqual(100.0, deserialized.GlobalVolume);
            Assert.AreEqual(string.Empty, deserialized.PrimaryDeviceName);
            Assert.AreEqual(string.Empty, deserialized.HeadphoneDeviceName);
        }

        // ── Save / Load ───────────────────────────────────────────────────────

        [TestMethod]
        public void SaveAndLoad_RoundTrip_PreservesAllValues()
        {
            var original = new AppSettings
            {
                PrimaryDeviceName = "Test Speaker",
                HeadphoneDeviceName = "Test Headphone",
                GlobalVolume = 77.0
            };
            original.Save();

            var loaded = AppSettings.Load();

            Assert.AreEqual(original.PrimaryDeviceName, loaded.PrimaryDeviceName);
            Assert.AreEqual(original.HeadphoneDeviceName, loaded.HeadphoneDeviceName);
            Assert.AreEqual(original.GlobalVolume, loaded.GlobalVolume);
        }

        [TestMethod]
        public void Load_WhenFileDoesNotExist_ReturnsDefaults()
        {
            var path = GetSettingsPath();
            if (File.Exists(path)) File.Delete(path);

            var loaded = AppSettings.Load();

            Assert.AreEqual(100.0, loaded.GlobalVolume);
            Assert.AreEqual(string.Empty, loaded.PrimaryDeviceName);
            Assert.AreEqual(string.Empty, loaded.HeadphoneDeviceName);
        }

        [TestMethod]
        public void Load_WithCorruptJson_ReturnsDefaults()
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ this is definitely not valid json }}}");

            var loaded = AppSettings.Load();

            Assert.AreEqual(100.0, loaded.GlobalVolume);
            Assert.AreEqual(string.Empty, loaded.PrimaryDeviceName);
        }

        [TestMethod]
        public void Load_WithEmptyFile_ReturnsDefaults()
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);

            var loaded = AppSettings.Load();

            Assert.AreEqual(100.0, loaded.GlobalVolume);
        }

        [TestMethod]
        public void Save_OverwritesPreviousSave()
        {
            new AppSettings { GlobalVolume = 50.0 }.Save();
            new AppSettings { GlobalVolume = 88.0 }.Save();

            var loaded = AppSettings.Load();

            Assert.AreEqual(88.0, loaded.GlobalVolume);
        }
    }
}
