using MySoundBoard.Controls;
using MySoundBoard.Managers;
using NAudio.Wave;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MenuItem = Wpf.Ui.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace MySoundBoard
{
    public partial class MainWindow : FluentWindow
    {
        private const string DefaultBoardName = "New Soundboard";

        private readonly AddButton _addButton;
        private static string AppDataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MySoundBoard");
        private static string BoardsDir => Path.Combine(AppDataDir, "SoundBoards");

        /// <summary>Global volume as a 0–1 multiplier.</summary>
        public float Volume { get; private set; } = 1.0f;
        public HotkeyManager? HotkeyManager { get; private set; }

        private string _savedSnapshot = string.Empty;
        // File backing the open board; null for a board that has never been saved or loaded.
        private string? _currentBoardPath;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounce;

        public static MainWindow Instance { get; private set; } = null!;

        private IEnumerable<SoundBoardButton> Buttons => SoundBoardGrid.Items.OfType<SoundBoardButton>();

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

            _searchDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _searchDebounce.Tick += (s, e) =>
            {
                _searchDebounce.Stop();
                ApplySearch(SearchBox.Text);
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HotkeyManager = new HotkeyManager(hwnd);
            LoadSettings();
            _savedSnapshot = BoardSnapshot();
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

        // Re-enumerate when a dropdown opens so devices plugged in after startup show up.
        private void DeviceCombo_DropDownOpened(object? sender, EventArgs e)
        {
            var current = DirectSoundOut.Devices.ToList();
            RefreshDeviceCombo(OutputDevice, current);
            RefreshDeviceCombo(HeadphoneDevice, current);
        }

        private static void RefreshDeviceCombo(System.Windows.Controls.ComboBox combo, List<DirectSoundDeviceInfo> devices)
        {
            var existing = combo.Items.OfType<DirectSoundDeviceInfo>().Select(d => d.Guid);
            if (existing.SequenceEqual(devices.Select(d => d.Guid))) return;

            var selected = (combo.SelectedItem as DirectSoundDeviceInfo)?.Description;
            combo.ItemsSource = devices;
            var match = devices.FirstOrDefault(d => d.Description == selected);
            combo.SelectedItem = match ?? devices.FirstOrDefault();
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
            var match = combo.Items.OfType<DirectSoundDeviceInfo>().FirstOrDefault(d => d.Description == name);
            if (match != null) combo.SelectedItem = match;
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
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }
            SaveSettings();
            foreach (var btn in Buttons) btn.Cleanup();
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
            // Close (not Application.Shutdown) so the unsaved-changes prompt can cancel the exit.
            menu.Items.Add("Exit", null, (s, e) => Dispatcher.Invoke(() => { RestoreWindow(); Close(); }));
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
            foreach (var btn in Buttons) btn.StopPlayback();
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void ApplySearch(string text)
        {
            foreach (var btn in Buttons)
                btn.Visibility = string.IsNullOrEmpty(text) ||
                    btn.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ── Grid management ───────────────────────────────────────────────────

        private void AddButton_Click(object sender, RoutedEventArgs e)
            => AddButtonToGrid(new SoundBoardButton());

        private void AddButtonToGrid(SoundBoardButton btn)
        {
            SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, btn);
            ApplySearch(SearchBox.Text);
        }

        public static void RemoveButton(SoundBoardButton button)
            => Instance.SoundBoardGrid.Items.Remove(button);

        public void AddButtonAfter(SoundBoardButton reference, SoundBoardButton newButton)
        {
            int idx = SoundBoardGrid.Items.IndexOf(reference);
            if (idx < 0) idx = SoundBoardGrid.Items.Count - 2;
            // Keep AddButton last
            int insertAt = Math.Min(idx + 1, SoundBoardGrid.Items.Count - 1);
            SoundBoardGrid.Items.Insert(insertAt, newButton);
            ApplySearch(SearchBox.Text);
        }

        // The dragged button takes the target's slot; the target shifts toward where the source was.
        public void MoveButton(SoundBoardButton source, SoundBoardButton target)
        {
            int srcIdx = SoundBoardGrid.Items.IndexOf(source);
            int tgtIdx = SoundBoardGrid.Items.IndexOf(target);
            if (srcIdx < 0 || tgtIdx < 0 || srcIdx == tgtIdx) return;
            SoundBoardGrid.Items.Remove(source);
            SoundBoardGrid.Items.Insert(tgtIdx, source);
        }

        private void ClearGrid()
        {
            foreach (var btn in Buttons) btn.Cleanup();
            SoundBoardGrid.Items.Clear();
            SoundBoardGrid.Items.Add(_addButton);
        }

        // ── Board files ───────────────────────────────────────────────────────

        private void InitializeLoadMenu()
        {
            Directory.CreateDirectory(BoardsDir);
            foreach (var file in new DirectoryInfo(BoardsDir).GetFiles("*.json").OrderBy(f => f.Name))
                AddLoadMenuEntry(file.FullName);
        }

        private MenuItem? FindLoadMenuEntry(string path)
            => LoadMenuItem.Items.OfType<MenuItem>()
                .FirstOrDefault(m => string.Equals(m.Tag as string, path, StringComparison.OrdinalIgnoreCase));

        // Windows paths are case-insensitive, so "Board" and "board" are the same entry.
        private void AddLoadMenuEntry(string path)
        {
            if (FindLoadMenuEntry(path) != null) return;
            var item = new MenuItem { Header = Path.GetFileNameWithoutExtension(path), Tag = path };
            item.Click += LoadMenuItem_Click;
            LoadMenuItem.Items.Add(item);
        }

        private static string BoardPath(string boardName) => Path.Combine(BoardsDir, $"{boardName}.json");

        private static string UniqueBoardName(string baseName)
        {
            string name = baseName;
            for (int i = 2; File.Exists(BoardPath(name)); i++)
                name = $"{baseName} {i}";
            return name;
        }

        // Serialized board + title, used to detect unsaved changes without tracking every edit.
        private string BoardSnapshot()
            => (SoundBoardTitle.Text ?? string.Empty) + "\n" + SerializeBoard();

        private string SerializeBoard()
        {
            var jArray = new JsonArray();
            foreach (var btn in Buttons) jArray.Add(btn.Serialize());
            return jArray.ToString();
        }

        private bool HasUnsavedChanges => BoardSnapshot() != _savedSnapshot;

        /// <summary>Offers to save unsaved changes. Returns false if the user cancelled or the save failed.</summary>
        private bool ConfirmDiscardChanges()
        {
            if (!HasUnsavedChanges) return true;
            var answer = MessageBox.Show(
                $"Save changes to '{SoundBoardTitle.Text}'?",
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return answer switch
            {
                MessageBoxResult.Yes => SaveBoard(),
                MessageBoxResult.No => true,
                _ => false,
            };
        }

        private void FileMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            DeleteBoardMenuItem.IsEnabled = _currentBoardPath != null && File.Exists(_currentBoardPath);
            LoadMenuItem.IsEnabled = LoadMenuItem.Items.Count > 0;
        }

        // ── New / Save / Load / Delete ────────────────────────────────────────

        private void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardChanges()) return;
            StartNewBoard();
        }

        private void StartNewBoard()
        {
            ClearGrid();
            SoundBoardTitle.Text = UniqueBoardName(DefaultBoardName);
            _currentBoardPath = null;
            _savedSnapshot = BoardSnapshot();
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e) => SaveBoard();

        private bool SaveBoard()
        {
            var boardName = SoundBoardTitle.Text?.Trim() ?? string.Empty;
            if (boardName.Length == 0 || boardName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(
                    "Please enter a board name that doesn't contain any of these characters: \\ / : * ? \" < > |",
                    "Invalid Board Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var path = BoardPath(boardName);
            bool isOtherBoard = !string.Equals(path, _currentBoardPath, StringComparison.OrdinalIgnoreCase);
            if (isOtherBoard && File.Exists(path))
            {
                var overwrite = MessageBox.Show(
                    $"A board named '{boardName}' already exists. Replace it?",
                    "Replace Board", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (overwrite != MessageBoxResult.Yes) return false;
            }

            try
            {
                Directory.CreateDirectory(BoardsDir);
                // Write to a temp file first so a failed save can't corrupt an existing board.
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, SerializeBoard());
                File.Move(tmp, path, overwrite: true);
                AddLoadMenuEntry(path);
                _currentBoardPath = path;
                _savedSnapshot = BoardSnapshot();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show($"Could not save '{boardName}':\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return false;
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: string path }) return;
            if (!ConfirmDiscardChanges()) return;

            // Parse the file before touching the grid so a bad file doesn't wipe the current board.
            JsonArray? arr;
            try
            {
                arr = JsonNode.Parse(File.ReadAllText(path)) as JsonArray;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                MessageBox.Show($"Could not load '{Path.GetFileName(path)}':\n{ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Old buttons must go first: their hotkeys would otherwise block the new ones from registering.
            ClearGrid();
            if (arr != null)
            {
                foreach (var jObj in arr)
                    if (jObj is JsonObject obj)
                        AddButtonToGrid(new SoundBoardButton(obj));
            }
            SoundBoardTitle.Text = Path.GetFileNameWithoutExtension(path);
            _currentBoardPath = path;
            _savedSnapshot = BoardSnapshot();
            WarnAboutUnregisteredHotkeys();
        }

        private void WarnAboutUnregisteredHotkeys()
        {
            var failed = Buttons.Where(b => b.HasUnregisteredHotkey).ToList();
            if (failed.Count == 0) return;
            var list = string.Join("\n", failed.Select(b => $"  {b.HotkeyDisplay}  ({b.Title})"));
            MessageBox.Show(
                $"These hotkeys are in use by another application and won't work until it releases them:\n\n{list}\n\n" +
                "They're kept with the board and shown struck through. Reload the board to try again, or assign a different hotkey.",
                "Hotkeys Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void DeleteBoardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBoardPath == null || !File.Exists(_currentBoardPath)) return;
            var name = Path.GetFileNameWithoutExtension(_currentBoardPath);
            var answer = MessageBox.Show(
                $"Permanently delete the board '{name}'?\nThe sound files themselves won't be touched.",
                "Delete Board", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                File.Delete(_currentBoardPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show($"Could not delete '{name}':\n{ex.Message}",
                    "Delete Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FindLoadMenuEntry(_currentBoardPath) is { } entry)
                LoadMenuItem.Items.Remove(entry);
            StartNewBoard();
        }

        private void OpenBoardsFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(BoardsDir);
            Process.Start(new ProcessStartInfo { FileName = BoardsDir, UseShellExecute = true });
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

        // ── Sort ──────────────────────────────────────────────────────────────

        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var buttons = Buttons.OrderBy(b => b.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            SoundBoardGrid.Items.Clear();
            foreach (var btn in buttons) SoundBoardGrid.Items.Add(btn);
            SoundBoardGrid.Items.Add(_addButton);
        }

        // ── Theme ─────────────────────────────────────────────────────────────

        private void DayMenuItem_Click(object sender, RoutedEventArgs e) => ApplyTheme(ApplicationTheme.Light);

        private void NightMenuItem_Click(object sender, RoutedEventArgs e) => ApplyTheme(ApplicationTheme.Dark);

        // Buttons bind their colours with resource references, so they follow the theme without a notification.
        private void ApplyTheme(ApplicationTheme theme)
        {
            DayMenuItem.IsChecked = theme == ApplicationTheme.Light;
            NightMenuItem.IsChecked = theme == ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, true);
        }

        // ── Volume ────────────────────────────────────────────────────────────

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Volume = (float)(VolumeSlider.Value / 100);
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
            if (SoundBoardGrid == null) return;
            foreach (var btn in Buttons) btn.UpdateVolume();
        }
    }
}
