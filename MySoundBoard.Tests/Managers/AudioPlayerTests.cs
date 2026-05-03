using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Managers;
using NAudio.Wave;

namespace MySoundBoard.Tests.Managers
{
    [TestClass]
    public class AudioPlayerTests
    {
        private static readonly string TestMp3 = Path.Combine("Resources", "test.mp3");

        private static DirectSoundDeviceInfo? _device;
        private AudioPlayer? _player;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            _device = DirectSoundOut.Devices.FirstOrDefault();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _player?.Dispose();
            _player = null;
        }

        private void SkipIfUnavailable()
        {
            if (_device == null)
                Assert.Inconclusive("No audio output device available on this machine.");
            if (!File.Exists(TestMp3))
                Assert.Inconclusive($"'{TestMp3}' not found in test output directory.");
        }

        private AudioPlayer CreatePlayer(float volume = 1.0f)
        {
            _player = new AudioPlayer(TestMp3, volume, _device!);
            return _player;
        }

        // ── Enum (no device needed) ───────────────────────────────────────────

        [TestMethod]
        public void PlaybackStopTypes_StoppedByUser_IsFirstValue()
        {
            Assert.AreEqual(0, (int)AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser);
        }

        [TestMethod]
        public void PlaybackStopTypes_StoppedReachingEndOfFile_IsSecondValue()
        {
            Assert.AreEqual(1, (int)AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile);
        }

        [TestMethod]
        public void PlaybackStopTypes_HasExactlyTwoValues()
        {
            Assert.AreEqual(2, Enum.GetValues<AudioPlayer.PlaybackStopTypes>().Length);
        }

        // ── Construction ──────────────────────────────────────────────────────

        [TestMethod]
        public void Constructor_SetsDefaultStopType_ToEndOfFile()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            Assert.AreEqual(AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile, player.PlaybackStopType);
        }

        [TestMethod]
        public void Constructor_SetsVolume_FromParameter()
        {
            SkipIfUnavailable();
            var player = CreatePlayer(volume: 0.75f);
            Assert.AreEqual(0.75f, player.Volume, 0.001f);
        }

        // ── Volume ────────────────────────────────────────────────────────────

        [TestMethod]
        public void SetVolume_UpdatesGetVolume()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            player.SetVolume(0.5f);
            Assert.AreEqual(0.5f, player.GetVolume(), 0.001f);
        }

        [TestMethod]
        public void SetVolume_ToZero_GetVolumeReturnsZero()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            player.SetVolume(0.0f);
            Assert.AreEqual(0.0f, player.GetVolume(), 0.001f);
        }

        [TestMethod]
        public void SetVolume_ToMaximum_GetVolumeReturnsOne()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            player.SetVolume(1.0f);
            Assert.AreEqual(1.0f, player.GetVolume(), 0.001f);
        }

        [TestMethod]
        public void Volume_Property_CanBeSetDirectly()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            player.Volume = 0.33f;
            Assert.AreEqual(0.33f, player.Volume, 0.001f);
        }

        // ── Playback position / length ────────────────────────────────────────

        [TestMethod]
        public void GetLengthInSeconds_ReturnsPositiveValue()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            Assert.IsTrue(player.GetLenghtInSeconds() > 0, "Track length must be greater than zero");
        }

        [TestMethod]
        public void GetPositionInSeconds_AtStart_IsNearZero()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            Assert.AreEqual(0.0, player.GetPositionInSeconds(), 0.05, "Initial position should be zero");
        }

        [TestMethod]
        public void SetPosition_ChangesCurrentPosition()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            double length = player.GetLenghtInSeconds();
            if (length < 2.0)
            {
                Assert.Inconclusive("test.mp3 is too short to test seek (need > 2 s).");
            }
            player.SetPosition(1.0);
            Assert.AreEqual(1.0, player.GetPositionInSeconds(), 0.1);
        }

        // ── PlaybackStopType ──────────────────────────────────────────────────

        [TestMethod]
        public void PlaybackStopType_CanBeOverridden()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            player.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
            Assert.AreEqual(AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser, player.PlaybackStopType);
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        [TestMethod]
        public void Dispose_CanBeCalledSafely()
        {
            SkipIfUnavailable();
            var player = new AudioPlayer(TestMp3, 1.0f, _device!);
            player.Dispose();
            // Second call should not throw
            player.Dispose();
            _player = null; // prevent double dispose in TestCleanup
        }

        [TestMethod]
        public void GetLengthInSeconds_AfterDispose_ReturnsZero()
        {
            SkipIfUnavailable();
            var player = new AudioPlayer(TestMp3, 1.0f, _device!);
            player.Dispose();
            Assert.AreEqual(0.0, player.GetLenghtInSeconds(), "After dispose, length should be 0");
            _player = null;
        }

        // ── Events ────────────────────────────────────────────────────────────

        [TestMethod]
        public void PlaybackStopped_EventCanBeSubscribed()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            bool fired = false;
            player.PlaybackStopped += () => fired = true;
            // We don't trigger playback in tests — just verify subscription doesn't throw
            Assert.IsFalse(fired);
        }

        [TestMethod]
        public void PlaybackResumed_EventCanBeSubscribed()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            bool fired = false;
            player.PlaybackResumed += () => fired = true;
            Assert.IsFalse(fired);
        }

        [TestMethod]
        public void PlaybackPaused_EventCanBeSubscribed()
        {
            SkipIfUnavailable();
            var player = CreatePlayer();
            bool fired = false;
            player.PlaybackPaused += () => fired = true;
            Assert.IsFalse(fired);
        }
    }
}
