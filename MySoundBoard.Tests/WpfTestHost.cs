using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySoundBoard.Tests
{
    /// <summary>
    /// Manages a single WPF Application + MainWindow running on a background STA thread.
    /// Required because SoundBoardButton.Initialize() accesses MainWindow.Instance.
    /// The MainWindow is never shown, so MainWindow_Loaded (and HotkeyManager init) never fires —
    /// all SoundBoardButton code that touches HotkeyManager uses null-conditional operators and is safe.
    /// </summary>
    internal static class WpfTestHost
    {
        private static Thread? _thread;
        private static Dispatcher? _dispatcher;
        private static bool _initialized;
        private static Exception? _initError;

        public static bool IsAvailable => _initialized && _initError == null;

        public static void EnsureInitialized()
        {
            if (_thread != null) return;

            var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                try
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    _ = new System.Windows.Application
                    {
                        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                    };
                    // Constructing MainWindow sets MainWindow.Instance, which SoundBoardButton needs.
                    _ = new MainWindow();
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    _initError = ex;
                    _initialized = true;
                }
                finally
                {
                    ready.Set();
                }

                Dispatcher.Run();
            });

            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();
            ready.Wait(TimeSpan.FromSeconds(15));
        }

        public static T Invoke<T>(Func<T> func) => _dispatcher!.Invoke(func);
        public static void Invoke(Action action) => _dispatcher!.Invoke(action);

        public static void SkipIfUnavailable()
        {
            if (!IsAvailable)
                Assert.Inconclusive($"WPF host unavailable: {_initError?.Message}");
        }
    }
}
