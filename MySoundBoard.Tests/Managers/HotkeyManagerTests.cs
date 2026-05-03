using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySoundBoard.Managers;

namespace MySoundBoard.Tests.Managers
{
    [TestClass]
    public class HotkeyManagerTests
    {
        [TestMethod]
        public void ModNone_IsZero()
        {
            Assert.AreEqual(0x0000u, HotkeyManager.MOD_NONE);
        }

        [TestMethod]
        public void ModAlt_IsOne()
        {
            Assert.AreEqual(0x0001u, HotkeyManager.MOD_ALT);
        }

        [TestMethod]
        public void ModControl_IsTwo()
        {
            Assert.AreEqual(0x0002u, HotkeyManager.MOD_CONTROL);
        }

        [TestMethod]
        public void ModShift_IsFour()
        {
            Assert.AreEqual(0x0004u, HotkeyManager.MOD_SHIFT);
        }

        [TestMethod]
        public void Constants_AllDistinct()
        {
            var values = new[] { HotkeyManager.MOD_NONE, HotkeyManager.MOD_ALT, HotkeyManager.MOD_CONTROL, HotkeyManager.MOD_SHIFT };
            Assert.AreEqual(values.Length, values.Distinct().Count(), "All modifier constants must be distinct");
        }

        [TestMethod]
        public void Constants_ArePowersOfTwo_ExceptNone()
        {
            Assert.AreEqual(0u, HotkeyManager.MOD_NONE);
            Assert.IsTrue(IsPowerOfTwo(HotkeyManager.MOD_ALT), "MOD_ALT should be a power of two");
            Assert.IsTrue(IsPowerOfTwo(HotkeyManager.MOD_CONTROL), "MOD_CONTROL should be a power of two");
            Assert.IsTrue(IsPowerOfTwo(HotkeyManager.MOD_SHIFT), "MOD_SHIFT should be a power of two");
        }

        [TestMethod]
        public void Constants_CanBeCombinedWithBitwiseOr()
        {
            uint combo = HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT;
            Assert.AreEqual(0x0003u, combo);
        }

        private static bool IsPowerOfTwo(uint value) => value != 0 && (value & (value - 1)) == 0;
    }
}
