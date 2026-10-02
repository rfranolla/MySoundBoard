using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MySoundBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // A second copy can't register the same global hotkeys, so only allow one.
            _singleInstanceMutex = new Mutex(true, "MySoundBoard.SingleInstance", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show("MySoundBoard is already running (check the system tray).",
                    "MySoundBoard", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        // Keep the app (and any hotkeys/audio) alive after a UI-thread error instead of crashing.
        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n{e.Exception.Message}",
                "MySoundBoard", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
