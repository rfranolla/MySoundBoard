using MySoundBoard.Managers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace MySoundBoard.Controls.Dialogs
{
    /// <summary>Captures a single key combination for a global hotkey.</summary>
    internal sealed class HotkeyInputWindow : FluentWindow
    {
        public uint CapturedModifiers { get; private set; }
        public uint CapturedKey { get; private set; }
        public string HotkeyText { get; private set; } = string.Empty;

        public HotkeyInputWindow()
        {
            Title = "Assign Hotkey";
            Width = 300;
            Height = 130;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            panel.Children.Add(new TextBlock
            {
                Text = "Press a key combination…",
                FontSize = 13,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "(Esc to cancel)",
                FontSize = 10,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Opacity = 0.6
            });
            Content = panel;

            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            e.Handled = true;

            if (key == Key.Escape) { Close(); return; }

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                    or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            uint mods = 0;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                mods |= HotkeyManager.MOD_CONTROL;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                mods |= HotkeyManager.MOD_ALT;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                mods |= HotkeyManager.MOD_SHIFT;

            CapturedModifiers = mods;
            CapturedKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            HotkeyText = FormatHotkey(mods, key);
            DialogResult = true;
        }

        private static string FormatHotkey(uint mods, Key key)
        {
            var parts = new List<string>();
            if ((mods & HotkeyManager.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & HotkeyManager.MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & HotkeyManager.MOD_SHIFT) != 0) parts.Add("Shift");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }
}
