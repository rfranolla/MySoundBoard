using MySoundBoard.Controls;
using MySoundBoard.Managers;
using NAudio.Wave;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Controls;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace MySoundBoard
{
    public partial class MainWindow : FluentWindow
    {
        private AddButton _addButton;
        private const string SoundBoardsDir = "SoundBoards";
        private static string AppDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MySoundBoard");

        public EventHandler<RoutedEventArgs>? ThemeChanged;
        public float Volume = 1.0f;
        public HotkeyManager? HotkeyManager { get; private set; }

        private System.Windows.Forms.NotifyIcon? _trayIcon;

        public static MainWindow Instance { get; private set; } = null!;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _addButton = new AddButton();
            _addButton.MainButton.Click += AddButton_Click;
            SoundBoardGrid.Items.Add(_addButton);

            InitDevices();
            InitializeLoadMenu();

            VolumeSlider.Value = 100;
            DataContext = this;

            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HotkeyManager = new HotkeyManager(hwnd);
            LoadSettings();
            InitTray();
        }

        // ── Devices ───────────────────────────────────────────────────────────

        private void InitDevices()
        {
            var list = DirectSoundOut.Devices.ToList();
            OutputDevice.ItemsSource = list;
            OutputDevice.DisplayMemberPath = "Description";
            OutputDevice.SelectedIndex = 0;
            HeadphoneDevice.ItemsSource = list;
            HeadphoneDevice.DisplayMemberPath = "Description";
            HeadphoneDevice.SelectedIndex = 0;
        }

        public static DirectSoundDeviceInfo? GetSelectedOutputDevice()
            => Instance.OutputDevice.SelectedItem as DirectSoundDeviceInfo;

        public static DirectSoundDeviceInfo? GetSelectedHeadphoneDevice()
            => Instance.HeadphoneDevice.SelectedItem as DirectSoundDeviceInfo;

        // ── Settings persistence ──────────────────────────────────────────────

        private void LoadSettings()
        {
            var s = AppSettings.Load();
            VolumeSlider.Value = s.GlobalVolume;
            SelectDeviceByName(OutputDevice, s.PrimaryDeviceName);
            SelectDeviceByName(HeadphoneDevice, s.HeadphoneDeviceName);
        }

        private static void SelectDeviceByName(System.Windows.Controls.ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (DirectSoundDeviceInfo d in combo.Items)
            {
                if (d.Description == name) { combo.SelectedItem = d; return; }
            }
        }

        private void SaveSettings()
        {
            new AppSettings
            {
                GlobalVolume = VolumeSlider.Value,
                PrimaryDeviceName = (OutputDevice.SelectedItem as DirectSoundDeviceInfo)?.Description ?? string.Empty,
                HeadphoneDeviceName = (HeadphoneDevice.SelectedItem as DirectSoundDeviceInfo)?.Description ?? string.Empty,
            }.Save();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveSettings();
            HotkeyManager?.Dispose();
            _trayIcon?.Dispose();
            base.OnClosing(e);
        }

        // ── System tray ───────────────────────────────────────────────────────

        private void InitTray()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon { Text = "MySoundBoard", Visible = false };

            var iconPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "SoundboardIcon.ico");
            if (File.Exists(iconPath))
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => Dispatcher.Invoke(RestoreWindow));
            menu.Items.Add("Stop All Sounds", null, (s, e) => Dispatcher.Invoke(StopAllSounds));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Dispatcher.Invoke(() => Application.Current.Shutdown()));
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => Dispatcher.Invoke(RestoreWindow);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
                if (_trayIcon != null) _trayIcon.Visible = true;
            }
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            if (_trayIcon != null) _trayIcon.Visible = false;
            Activate();
        }

        // ── Stop All ──────────────────────────────────────────────────────────

        private void StopAllButton_Click(object sender, RoutedEventArgs e) => StopAllSounds();

        public void StopAllSounds()
        {
            foreach (var item in SoundBoardGrid.Items)
            {
                if (item is SoundBoardButton btn)
                    btn.StopPlayback();
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                StopAllSounds();
                e.Handled = true;
            }
        }

        // ── Search / filter ───────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = SearchBox.Text;
            foreach (var item in SoundBoardGrid.Items)
            {
                if (item is SoundBoardButton btn)
                    btn.Visibility = string.IsNullOrEmpty(text) ||
                        btn.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
        }

        // ── Grid management ───────────────────────────────────────────────────

        private void AddButton_Click(object sender, RoutedEventArgs e)
            => AddButtonToGrid(new SoundBoardButton());

        private void AddButtonToGrid(SoundBoardButton btn)
            => SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, btn);

        public static void RemoveButton(SoundBoardButton button)
            => Instance.SoundBoardGrid.Items.Remove(button);

        public void AddButtonAfter(SoundBoardButton reference, SoundBoardButton newButton)
        {
            int idx = SoundBoardGrid.Items.IndexOf(reference);
            if (idx < 0) idx = SoundBoardGrid.Items.Count - 2;
            // Keep AddButton last
            int insertAt = Math.Min(idx + 1, SoundBoardGrid.Items.Count - 1);
            SoundBoardGrid.Items.Insert(insertAt, newButton);
        }

        public void MoveButton(SoundBoardButton source, SoundBoardButton target)
        {
            int srcIdx = SoundBoardGrid.Items.IndexOf(source);
            int tgtIdx = SoundBoardGrid.Items.IndexOf(target);
            if (srcIdx < 0 || tgtIdx < 0 || srcIdx == tgtIdx) return;
            SoundBoardGrid.Items.Remove(source);
            if (tgtIdx > srcIdx) tgtIdx--;
            SoundBoardGrid.Items.Insert(tgtIdx, source);
        }

        // ── Load menu ─────────────────────────────────────────────────────────

        private void InitializeLoadMenu()
        {
            var dirPath = Path.Combine(AppDataDir, SoundBoardsDir);
            Directory.CreateDirectory(dirPath);
            foreach (var file in new DirectoryInfo(dirPath).GetFiles("*.json"))
                AddLoadMenuEntry(file);
        }

        private void AddLoadMenuEntry(FileInfo file)
        {
            var boardName = file.Name.Split(".")[0];
            foreach (MenuItem existing in LoadMenuItem.Items)
                if (existing.Header?.ToString() == boardName) return;

            var item = new MenuItem { Header = boardName, DataContext = file };
            item.Click += LoadMenuItem_Click;
            LoadMenuItem.Items.Add(item);
        }

        // ── Save / Load ───────────────────────────────────────────────────────

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var jArray = new JsonArray();
            foreach (var item in SoundBoardGrid.Items)
                if (item is SoundBoardButton btn) jArray.Add(btn.Serialize());

            var boardName = SoundBoardTitle.Text;
            var path = Path.Combine(AppDataDir, SoundBoardsDir, $"{boardName}.json");
            File.WriteAllText(path, jArray.ToString());
            AddLoadMenuEntry(new FileInfo(path));
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearGrid();
            if (sender is not MenuItem menuItem) return;
            var file = menuItem.DataContext as FileInfo;
            if (file == null) return;

            string json = File.ReadAllText(file.FullName);
            if (JsonArray.Parse(json) is JsonArray arr)
            {
                foreach (var jObj in arr)
                    AddButtonToGrid(new SoundBoardButton((JsonObject)jObj!));
            }
            SoundBoardTitle.Text = menuItem.Header?.ToString();
        }

        private void ClearGrid()
        {
            foreach (var item in SoundBoardGrid.Items)
                if (item is SoundBoardButton btn) btn.Cleanup();
            SoundBoardGrid.Items.Clear();
            SoundBoardGrid.Items.Add(_addButton);
        }

        // ── Sort ──────────────────────────────────────────────────────────────

        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var buttons = SoundBoardGrid.Items
                .OfType<SoundBoardButton>()
                .OrderBy(b => b.Title)
                .ToList();

            SoundBoardGrid.Items.Clear();
            foreach (var btn in buttons) SoundBoardGrid.Items.Add(btn);
            SoundBoardGrid.Items.Add(_addButton);
        }

        // ── Theme ─────────────────────────────────────────────────────────────

        private void DayMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DayMenuItem.IsChecked = true;
            NightMenuItem.IsChecked = false;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                Wpf.Ui.Appearance.ApplicationTheme.Light,
                Wpf.Ui.Controls.WindowBackdropType.Mica, true);
            ThemeChanged?.Invoke(this, new RoutedEventArgs());
        }

        private void NightMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DayMenuItem.IsChecked = false;
            NightMenuItem.IsChecked = true;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                Wpf.Ui.Appearance.ApplicationTheme.Dark,
                Wpf.Ui.Controls.WindowBackdropType.Mica, true);
            ThemeChanged?.Invoke(this, new RoutedEventArgs());
        }

        // ── Volume ────────────────────────────────────────────────────────────

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Volume = (float)VolumeSlider.Value;
            var normalized = Volume / 100f;
            foreach (var item in SoundBoardGrid.Items)
                if (item is SoundBoardButton btn) btn.UpdateVolume(normalized);
        }
    }
}
