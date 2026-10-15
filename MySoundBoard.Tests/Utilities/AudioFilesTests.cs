using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Utilities;

namespace MySoundBoard.Tests.Utilities
{
    [TestClass]
    public class AudioFilesTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "MySoundBoard.AudioFilesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_dir, recursive: true);

        private string Touch(string relativePath)
        {
            var path = Path.Combine(_dir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }

        [TestMethod]
        [DataRow("a.mp3", true)]
        [DataRow("a.WAV", true)]
        [DataRow("a.ogg", true)]
        [DataRow("a.txt", false)]
        [DataRow("mp3", false)]
        public void IsSupported_ChecksExtensionCaseInsensitively(string path, bool expected)
            => Assert.AreEqual(expected, AudioFiles.IsSupported(path));

        [TestMethod]
        public void Expand_KeepsSupportedFilesInOrder_AndSkipsOthers()
        {
            var b = Touch("b.mp3");
            var notes = Touch("notes.txt");
            var a = Touch("a.wav");

            CollectionAssert.AreEqual(new[] { b, a }, AudioFiles.Expand(new[] { b, notes, a }));
        }

        [TestMethod]
        public void Expand_Folder_AddsTopLevelAudioSortedByName()
        {
            var two = Touch(Path.Combine("pack", "2.mp3"));
            var one = Touch(Path.Combine("pack", "1.ogg"));
            Touch(Path.Combine("pack", "readme.txt"));
            Touch(Path.Combine("pack", "nested", "3.mp3"));

            CollectionAssert.AreEqual(new[] { one, two }, AudioFiles.Expand(new[] { Path.Combine(_dir, "pack") }));
        }

        [TestMethod]
        public void Expand_MissingPath_IsIgnored()
            => Assert.AreEqual(0, AudioFiles.Expand(new[] { Path.Combine(_dir, "gone.mp3") }).Count);

        [TestMethod]
        public void NearestExistingDirectory_WalksUpToAFolderThatExists()
        {
            var missing = Path.Combine(_dir, "moved", "away", "clip.mp3");
            Assert.AreEqual(_dir, AudioFiles.NearestExistingDirectory(missing));
        }
    }
}
