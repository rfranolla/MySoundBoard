using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MySoundBoard.Managers
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        public const uint MOD_NONE    = 0x0000;
        public const uint MOD_ALT     = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT   = 0x0004;

        private HwndSource? _hwndSource;
        private readonly Dictionary<int, Action> _hotkeys = new();
        private int _nextId = 9000;

        public HotkeyManager(IntPtr hwnd)
        {
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        public int Register(uint modifiers, uint vk, Action callback)
        {
            if (_hwndSource == null) return -1;
            int id = _nextId++;
            if (RegisterHotKey(_hwndSource.Handle, id, modifiers, vk))
            {
                _hotkeys[id] = callback;
                return id;
            }
            _nextId--;
            return -1;
        }

        public void Unregister(int id)
        {
            if (id < 0 || _hwndSource == null || !_hotkeys.ContainsKey(id)) return;
            UnregisterHotKey(_hwndSource.Handle, id);
            _hotkeys.Remove(id);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _hotkeys.TryGetValue(wParam.ToInt32(), out var action))
            {
                action();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hwndSource == null) return;
            foreach (var id in _hotkeys.Keys.ToList())
                UnregisterHotKey(_hwndSource.Handle, id);
            _hwndSource.RemoveHook(WndProc);
        }
    }
}
