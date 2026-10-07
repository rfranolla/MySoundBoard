using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MySoundBoard.Managers
{
    public interface IHotkeyRegistrar
    {
        /// <summary>Registers a global hotkey. Returns its id, or -1 if the combination is already taken.</summary>
        int Register(uint modifiers, uint vk, Action callback);
        void Unregister(int id);
    }

    public class HotkeyManager : IHotkeyRegistrar, IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int WM_HOTKEY = 0x0312;

        public const uint MOD_NONE    = 0x0000;
        public const uint MOD_ALT     = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT   = 0x0004;
        // Holding the keys fires once instead of auto-repeating, so a held toggle hotkey doesn't flicker.
        private const uint MOD_NOREPEAT = 0x4000;

        private HwndSource? _hwndSource;
        private readonly Dictionary<int, Action> _hotkeys = new();
        private int _nextId = 9000;

        public HotkeyManager(IntPtr hwnd)
        {
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        /// <summary>True while the key is physically held, whichever window has focus.</summary>
        public static bool IsKeyDown(uint vk) => (GetAsyncKeyState((int)vk) & 0x8000) != 0;

        public int Register(uint modifiers, uint vk, Action callback)
        {
            if (_hwndSource == null) return -1;
            int id = _nextId++;
            if (RegisterHotKey(_hwndSource.Handle, id, modifiers | MOD_NOREPEAT, vk))
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
            _hotkeys.Clear();
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}
