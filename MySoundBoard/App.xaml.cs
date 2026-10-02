using System.Windows;
using System.Windows.Threading;

namespace MySoundBoard
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
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
