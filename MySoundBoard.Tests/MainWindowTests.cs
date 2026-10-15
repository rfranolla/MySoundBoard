using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySoundBoard.Tests
{
    [TestClass]
    public class MainWindowTests
    {
        [ClassInitialize]
        public static void ClassInit(TestContext _) => WpfTestHost.EnsureInitialized();

        // Catches XAML that only fails at load time, such as a misspelled icon name.
        [TestMethod]
        public void Constructor_LoadsXamlWithoutThrowing()
        {
            WpfTestHost.SkipIfUnavailable();
            WpfTestHost.Invoke(() => { _ = new MainWindow(); });
        }
    }
}
