using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Controls;

namespace MySoundBoard.Tests.Controls
{
    [TestClass]
    public class SoundBoardButtonTests
    {
        [ClassInitialize]
        public static void ClassInit(TestContext _) => WpfTestHost.EnsureInitialized();

        private void Skip() => WpfTestHost.SkipIfUnavailable();

        // ── Serialize shape ───────────────────────────────────────────────────

        [TestMethod]
        public void Serialize_ContainsAllExpectedKeys()
        {
            Skip();
            var json = WpfTestHost.Invoke(() => new SoundBoardButton().Serialize());

            var expected = new[]
            {
                "LoopSound", "PlayThroughHeadphones", "soundFile", "Title",
                "CustomPlayIcon", "ButtonVolume", "ButtonColor",
                "FadeInSeconds", "FadeOutSeconds", "FadeEnabled", "AutoStopSeconds"
            };
            foreach (var key in expected)
                Assert.IsTrue(json.ContainsKey(key), $"Expected key '{key}' missing from serialized output");
        }

        [TestMethod]
        public void Serialize_DefaultValues_MatchExpected()
        {
            Skip();
            var json = WpfTestHost.Invoke(() => new SoundBoardButton().Serialize());

            Assert.AreEqual(false, json["LoopSound"]!.GetValue<bool>());
            Assert.AreEqual(false, json["PlayThroughHeadphones"]!.GetValue<bool>());
            Assert.AreEqual(true, json["FadeEnabled"]!.GetValue<bool>());
            Assert.AreEqual(1.0f, json["ButtonVolume"]!.GetValue<float>(), 0.001f);
            Assert.AreEqual(0.0, json["FadeInSeconds"]!.GetValue<double>(), 0.001);
            Assert.AreEqual(0.0, json["FadeOutSeconds"]!.GetValue<double>(), 0.001);
            Assert.AreEqual(0.0, json["AutoStopSeconds"]!.GetValue<double>(), 0.001);
            Assert.AreEqual(string.Empty, json["soundFile"]!.GetValue<string>());
            Assert.AreEqual(string.Empty, json["ButtonColor"]!.GetValue<string>());
        }

        [TestMethod]
        public void Serialize_WithoutHotkey_DoesNotIncludeHotkeyKeys()
        {
            Skip();
            var json = WpfTestHost.Invoke(() => new SoundBoardButton().Serialize());

            Assert.IsFalse(json.ContainsKey("HotkeyModifiers"), "Default button should not include HotkeyModifiers");
            Assert.IsFalse(json.ContainsKey("HotkeyVirtualKey"), "Default button should not include HotkeyVirtualKey");
            Assert.IsFalse(json.ContainsKey("HotkeyDisplay"), "Default button should not include HotkeyDisplay");
        }

        // ── Round-trip field preservation ─────────────────────────────────────

        [TestMethod]
        public void RoundTrip_LoopSound_True_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(loop: true));
            Assert.AreEqual(true, result["LoopSound"]!.GetValue<bool>());
        }

        [TestMethod]
        public void RoundTrip_LoopSound_False_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(loop: false));
            Assert.AreEqual(false, result["LoopSound"]!.GetValue<bool>());
        }

        [TestMethod]
        public void RoundTrip_PlayThroughHeadphones_True_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(headphones: true));
            Assert.AreEqual(true, result["PlayThroughHeadphones"]!.GetValue<bool>());
        }

        [TestMethod]
        public void RoundTrip_FadeEnabled_False_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(fadeEnabled: false));
            Assert.AreEqual(false, result["FadeEnabled"]!.GetValue<bool>());
        }

        [TestMethod]
        public void RoundTrip_FadeEnabled_True_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(fadeEnabled: true));
            Assert.AreEqual(true, result["FadeEnabled"]!.GetValue<bool>());
        }

        [TestMethod]
        public void RoundTrip_FadeInSeconds_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(fadeIn: 3.5));
            Assert.AreEqual(3.5, result["FadeInSeconds"]!.GetValue<double>(), 0.001);
        }

        [TestMethod]
        public void RoundTrip_FadeOutSeconds_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(fadeOut: 2.0));
            Assert.AreEqual(2.0, result["FadeOutSeconds"]!.GetValue<double>(), 0.001);
        }

        [TestMethod]
        public void RoundTrip_AutoStopSeconds_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(autoStop: 45.0));
            Assert.AreEqual(45.0, result["AutoStopSeconds"]!.GetValue<double>(), 0.001);
        }

        [TestMethod]
        public void RoundTrip_ButtonVolume_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(volume: 0.65f));
            Assert.AreEqual(0.65f, result["ButtonVolume"]!.GetValue<float>(), 0.001f);
        }

        [TestMethod]
        public void RoundTrip_ButtonColor_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(color: "#FF5500"));
            Assert.AreEqual("#FF5500", result["ButtonColor"]!.GetValue<string>());
        }

        [TestMethod]
        public void RoundTrip_Title_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(title: "Explosion Sound"));
            Assert.AreEqual("Explosion Sound", result["Title"]!.GetValue<string>());
        }

        [TestMethod]
        public void RoundTrip_SoundFile_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(soundFile: @"C:\sounds\test.mp3"));
            Assert.AreEqual(@"C:\sounds\test.mp3", result["soundFile"]!.GetValue<string>());
        }

        [TestMethod]
        public void RoundTrip_CustomPlayIcon_Preserved()
        {
            Skip();
            var result = RoundTrip(BuildJsonObject(customPlayIcon: "Pause48"));
            Assert.AreEqual("Pause48", result["CustomPlayIcon"]!.GetValue<string>());
        }

        [TestMethod]
        public void RoundTrip_AllFieldsTogether_Preserved()
        {
            Skip();
            var input = BuildJsonObject(
                loop: true, headphones: true, soundFile: @"C:\test.wav", title: "Big Bang",
                volume: 0.8f, color: "#123456", fadeIn: 1.5, fadeOut: 2.5,
                fadeEnabled: false, autoStop: 20.0, customPlayIcon: "Stop24");

            var result = RoundTrip(input);

            Assert.AreEqual(true,    result["LoopSound"]!.GetValue<bool>());
            Assert.AreEqual(true,    result["PlayThroughHeadphones"]!.GetValue<bool>());
            Assert.AreEqual(@"C:\test.wav", result["soundFile"]!.GetValue<string>());
            Assert.AreEqual("Big Bang",     result["Title"]!.GetValue<string>());
            Assert.AreEqual(0.8f,    result["ButtonVolume"]!.GetValue<float>(), 0.001f);
            Assert.AreEqual("#123456", result["ButtonColor"]!.GetValue<string>());
            Assert.AreEqual(1.5,     result["FadeInSeconds"]!.GetValue<double>(), 0.001);
            Assert.AreEqual(2.5,     result["FadeOutSeconds"]!.GetValue<double>(), 0.001);
            Assert.AreEqual(false,   result["FadeEnabled"]!.GetValue<bool>());
            Assert.AreEqual(20.0,    result["AutoStopSeconds"]!.GetValue<double>(), 0.001);
        }

        // ── Backward compatibility ────────────────────────────────────────────

        [TestMethod]
        public void Deserialized_WithMissingFadeEnabled_DefaultsToTrue()
        {
            Skip();
            // Simulate an old save file that pre-dates the FadeEnabled field
            var oldFormat = new JsonObject
            {
                ["LoopSound"] = false,
                ["PlayThroughHeadphones"] = false,
                ["soundFile"] = "",
                ["Title"] = "Old Button",
                ["CustomPlayIcon"] = "Play48",
                ["ButtonVolume"] = 1.0f,
                ["ButtonColor"] = "",
                ["FadeInSeconds"] = 2.0,
                ["FadeOutSeconds"] = 1.0,
                ["AutoStopSeconds"] = 0.0
                // FadeEnabled intentionally absent
            };

            var result = WpfTestHost.Invoke(() => new SoundBoardButton(oldFormat).Serialize());

            Assert.AreEqual(true, result["FadeEnabled"]!.GetValue<bool>(),
                "FadeEnabled should default to true for old saves without the key");
        }

        [TestMethod]
        public void Deserialized_WithMissingOptionalKeys_DoesNotThrow()
        {
            Skip();
            // Minimal valid JSON — only required-ish fields
            var minimal = new JsonObject
            {
                ["soundFile"] = "anything.mp3",
                ["Title"] = "Minimal"
            };

            // Should not throw
            WpfTestHost.Invoke(() => { _ = new SoundBoardButton(minimal); });
        }

        // ── Title property ────────────────────────────────────────────────────

        [TestMethod]
        public void Title_Property_MatchesTitleFromJson()
        {
            Skip();
            WpfTestHost.Invoke(() =>
            {
                var btn = new SoundBoardButton(BuildJsonObject(title: "My Sound Effect"));
                Assert.AreEqual("My Sound Effect", btn.Title);
            });
        }

        [TestMethod]
        public void Title_DefaultButton_IsEmptyString()
        {
            Skip();
            WpfTestHost.Invoke(() =>
            {
                var btn = new SoundBoardButton();
                Assert.AreEqual(string.Empty, btn.Title);
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private JsonObject RoundTrip(JsonObject input) =>
            WpfTestHost.Invoke(() => new SoundBoardButton(input).Serialize());

        private static JsonObject BuildJsonObject(
            bool loop = false, bool headphones = false, string soundFile = "",
            string title = "", float volume = 1.0f, string color = "",
            double fadeIn = 0.0, double fadeOut = 0.0, bool fadeEnabled = true,
            double autoStop = 0.0, string customPlayIcon = "Play48")
        {
            return new JsonObject
            {
                ["LoopSound"] = loop,
                ["PlayThroughHeadphones"] = headphones,
                ["soundFile"] = soundFile,
                ["Title"] = title,
                ["CustomPlayIcon"] = customPlayIcon,
                ["ButtonVolume"] = volume,
                ["ButtonColor"] = color,
                ["FadeInSeconds"] = fadeIn,
                ["FadeOutSeconds"] = fadeOut,
                ["FadeEnabled"] = fadeEnabled,
                ["AutoStopSeconds"] = autoStop
            };
        }
    }
}
