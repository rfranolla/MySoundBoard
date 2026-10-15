using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySoundBoard.Tests
{
    /// <summary>
    /// Runs a single WPF Application on a background STA thread so tests can create controls.
    /// Buttons are tested against <see cref="FakeSoundBoardHost"/>, so no MainWindow is needed here.
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
