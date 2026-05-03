using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Utilities;

namespace MySoundBoard.Tests.Utilities
{
    [TestClass]
    public class IconNameFormatterTests
    {
        // ── Suffix stripping ──────────────────────────────────────────────────

        [TestMethod]
        public void SimpleIcon_StripsSize48Suffix()
        {
            Assert.AreEqual("Add", IconNameFormatter.FormatDisplayName("Add48"));
        }

        [TestMethod]
        public void SimpleIcon_NoSuffix_ReturnsNameUnchanged()
        {
            Assert.AreEqual("Play", IconNameFormatter.FormatDisplayName("Play"));
        }

        // ── PascalCase splitting ──────────────────────────────────────────────

        [TestMethod]
        public void TwoWordPascalCase_InsertsSpace()
        {
            Assert.AreEqual("Chevron Left", IconNameFormatter.FormatDisplayName("ChevronLeft48"));
        }

        [TestMethod]
        public void ThreeWordPascalCase_InsertsSpaces()
        {
            Assert.AreEqual("Arrow Down Left", IconNameFormatter.FormatDisplayName("ArrowDownLeft48"));
        }

        [TestMethod]
        public void PascalCaseWithoutSuffix_StillInsertsSpaces()
        {
            Assert.AreEqual("Some Icon", IconNameFormatter.FormatDisplayName("SomeIcon"));
        }

        // ── Acronyms ──────────────────────────────────────────────────────────

        [TestMethod]
        public void AcronymFollowedByWord_SplitsCorrectly()
        {
            Assert.AreEqual("TV Monitor", IconNameFormatter.FormatDisplayName("TVMonitor48"));
        }

        // ── Digits in name ────────────────────────────────────────────────────

        [TestMethod]
        public void DigitInName_StripsOnly48Suffix()
        {
            Assert.AreEqual("Music Note 1", IconNameFormatter.FormatDisplayName("MusicNote148"));
        }

        // ── Edge cases ────────────────────────────────────────────────────────

        [TestMethod]
        public void EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, IconNameFormatter.FormatDisplayName(string.Empty));
        }

        [TestMethod]
        public void SingleWord_StripsSuffixOnly()
        {
            Assert.AreEqual("Play", IconNameFormatter.FormatDisplayName("Play48"));
        }
    }
}
