using MySoundBoard.Controls;
using MySoundBoard.Controls.Dialogs;
using MySoundBoard.Managers;
using MySoundBoard.Utilities;
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
    public partial class MainWindow : FluentWindow, ISoundBoardHost
    {
        private const string DefaultBoardName = "New Soundboard";

        private readonly AddButton _addButton;
        private readonly AppSettings _settings;

        /// <summary>Global volume as a 0–1 multiplier.</summary>
        public float Volume { get; private set; } = 1.0f;
        public HotkeyManager? HotkeyManager { get; private set; }

        private string _savedSnapshot = string.Empty;
        // File backing the open board; null for a board that has never been saved or loaded.
        private string? _currentBoardPath;
        private int _stopAllHotkeyId = -1;
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounce;

        private IEnumerable<SoundBoardButton> Buttons => SoundBoardGrid.Items.OfType<SoundBoardButton>();

        public MainWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();

            _addButton = new AddButton();
            _addButton.MainButton.Click += AddButton_Click;
            SoundBoardGrid.Items.Add(_addButton);

            InitDevices();
            InitializeLoadMenu();

            // Applied before the window is shown so it opens in the right place and theme.
            VolumeSlider.Value = _settings.GlobalVolume;
            SelectDeviceByName(OutputDevice, _settings.PrimaryDeviceName);
            SelectDeviceByName(HeadphoneDevice, _settings.HeadphoneDeviceName);
            RestoreWindowBounds(_settings.Window);
            if (_settings.Theme == nameof(ApplicationTheme.Light))
                ApplyTheme(ApplicationTheme.Light);
            else
                UpdateThemeChecks(ApplicationTheme.Dark);

            DataContext = this;

            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
            // Files may have been moved back (or a drive reconnected) while the app was in the background.
            Activated += (s, e) =>
            {
                foreach (var btn in Buttons) btn.RefreshMissingFileState();
            };

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

        // Hotkeys need the window handle, so anything that registers them waits for Loaded.
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HotkeyManager = new HotkeyManager(hwnd);

            if (!RegisterStopAllHotkey() && _settings.StopAllHotkey != null)
                MessageBox.Show(
                    $"The Stop All hotkey '{_settings.StopAllHotkey.Display}' is in use by another application, so it won't work this session.",
                    "Hotkey Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);

            if (!string.IsNullOrEmpty(_settings.LastBoardPath) && File.Exists(_settings.LastBoardPath))
                LoadBoardFile(_settings.LastBoardPath);
            _savedSnapshot = BoardSnapshot();
            InitTray();
        }

        // ── ISoundBoardHost ───────────────────────────────────────────────────

        public DirectSoundDeviceInfo? PrimaryDevice => OutputDevice.SelectedItem as DirectSoundDeviceInfo;
        public DirectSoundDeviceInfo? SecondaryDevice => HeadphoneDevice.SelectedItem as DirectSoundDeviceInfo;
        public IHotkeyRegistrar? Hotkeys => HotkeyManager;

        public void RemoveButton(SoundBoardButton button) => SoundBoardGrid.Items.Remove(button);

        public void InsertButtonAfter(SoundBoardButton reference, SoundBoardButton newButton)
        {
            SoundBoardGrid.Items.Insert(InsertIndexAfter(reference), newButton);
            newButton.ActivateHotkey();
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

        public void AddSoundFiles(IEnumerable<string> paths, SoundBoardButton? after)
        {
            var files = AudioFiles.Expand(paths);
            if (files.Count == 0)
            {
                MessageBox.Show("None of those are supported audio files (MP3, WAV or OGG).",
                    "Add Sounds", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int insertAt = after != null ? InsertIndexAfter(after) : SoundBoardGrid.Items.Count - 1;
            foreach (var file in files)
                SoundBoardGrid.Items.Insert(insertAt++, SoundBoardButton.ForFile(this, file));
            ApplySearch(SearchBox.Text);
        }

        public int RelinkMissingSounds(string oldDirectory, string newDirectory)
        {
            int relinked = 0;
            foreach (var btn in Buttons)
                if (btn.TryRelink(oldDirectory, newDirectory)) relinked++;
            return relinked;
        }

        // Keeps the AddButton last.
        private int InsertIndexAfter(SoundBoardButton reference)
        {
            int idx = SoundBoardGrid.Items.IndexOf(reference);
            return idx < 0 ? SoundBoardGrid.Items.Count - 1 : Math.Min(idx + 1, SoundBoardGrid.Items.Count - 1);
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

        private static void SelectDeviceByName(System.Windows.Controls.ComboBox combo, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var match = combo.Items.OfType<DirectSoundDeviceInfo>().FirstOrDefault(d => d.Description == name);
            if (match != null) combo.SelectedItem = match;
        }

        // ── Settings and window placement ─────────────────────────────────────

        private void RestoreWindowBounds(WindowBounds? bounds)
        {
            if (bounds == null || bounds.Width < 300 || bounds.Height < 200) return;

            // Skip if the monitor it was on is gone: the title bar must stay reachable.
            var saved = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            var desktop = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var visible = Rect.Intersect(saved, desktop);
            if (visible.IsEmpty || visible.Width < 100 || visible.Height < 50) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            if (bounds.Maximized) WindowState = WindowState.Maximized;
        }

        private void SaveSettings()
        {
            _settings.GlobalVolume = VolumeSlider.Value;
            _settings.PrimaryDeviceName = PrimaryDevice?.Description ?? string.Empty;
            _settings.HeadphoneDeviceName = SecondaryDevice?.Description ?? string.Empty;
            _settings.LastBoardPath = _currentBoardPath ?? string.Empty;

            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty)
            {
                _settings.Window = new WindowBounds
                {
                    Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height,
                    Maximized = WindowState == WindowState.Maximized
                };
            }
            _settings.Save();
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

        /// <summary>Registers the saved Stop All hotkey. Returns false if one is set but couldn't be registered.</summary>
        private bool RegisterStopAllHotkey()
        {
            if (_stopAllHotkeyId >= 0)
            {
                HotkeyManager?.Unregister(_stopAllHotkeyId);
                _stopAllHotkeyId = -1;
            }
            if (_settings.StopAllHotkey is { VirtualKey: > 0 } hotkey && HotkeyManager != null)
                _stopAllHotkeyId = HotkeyManager.Register(hotkey.Modifiers, hotkey.VirtualKey,
                    () => Dispatcher.Invoke(StopAllSounds));
            UpdateStopAllTooltip();
            return _settings.StopAllHotkey == null || _stopAllHotkeyId >= 0;
        }

        private void UpdateStopAllTooltip()
        {
            StopAllButton.ToolTip = _stopAllHotkeyId >= 0
                ? $"Stop all playing sounds (Esc, or {_settings.StopAllHotkey!.Display} from anywhere)"
                : "Stop all playing sounds (Esc)";
        }

        private void ToolsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            StopAllHotkeyMenuItem.Header = _settings.StopAllHotkey == null
                ? "Set Stop All _Hotkey…"
                : $"Set Stop All _Hotkey… ({_settings.StopAllHotkey.Display})";
            ClearStopAllHotkeyMenuItem.IsEnabled = _settings.StopAllHotkey != null;
        }

        private void SetStopAllHotkeyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new HotkeyInputWindow { Owner = this };
            if (win.ShowDialog() != true) return;

            var previous = _settings.StopAllHotkey;
            _settings.StopAllHotkey = new HotkeySetting
            {
                Modifiers = win.CapturedModifiers, VirtualKey = win.CapturedKey, Display = win.HotkeyText
            };
            if (!RegisterStopAllHotkey())
            {
                _settings.StopAllHotkey = previous;
                RegisterStopAllHotkey();
                MessageBox.Show(
                    $"The hotkey '{win.HotkeyText}' is already in use by another application or button.",
                    "Hotkey Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveSettings();
        }

        private void ClearStopAllHotkeyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _settings.StopAllHotkey = null;
            RegisterStopAllHotkey();
            SaveSettings();
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
        {
            SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, new SoundBoardButton(this));
            ApplySearch(SearchBox.Text);
        }

        private void ClearGrid()
        {
            foreach (var btn in Buttons) btn.Cleanup();
            SoundBoardGrid.Items.Clear();
            SoundBoardGrid.Items.Add(_addButton);
        }

        // Drops onto a tile are handled by the tile; this covers the empty space around them.
        private void Grid_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy
                : e.Data.GetDataPresent(typeof(SoundBoardButton)) ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void Grid_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Handled) return;
            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
            {
                AddSoundFiles(files, null);
            }
            else if (e.Data.GetData(typeof(SoundBoardButton)) is SoundBoardButton source)
            {
                // Dropped past the last tile: move it to the end.
                SoundBoardGrid.Items.Remove(source);
                SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, source);
            }
            e.Handled = true;
        }

        private void AddSoundsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Add Sounds",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Filter = AudioFiles.DialogFilter,
                Multiselect = true,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == true)
                AddSoundFiles(dialog.FileNames, null);
        }

        // ── Board files ───────────────────────────────────────────────────────

        private void InitializeLoadMenu()
        {
            Directory.CreateDirectory(AppPaths.BoardsDirectory);
            foreach (var file in new DirectoryInfo(AppPaths.BoardsDirectory).GetFiles("*.json").OrderBy(f => f.Name))
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

        private static string BoardPath(string boardName) => Path.Combine(AppPaths.BoardsDirectory, $"{boardName}.json");

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
                Directory.CreateDirectory(AppPaths.BoardsDirectory);
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
            LoadBoardFile(path);
        }

        /// <summary>
        /// Replaces the grid with the board in <paramref name="path"/>. Every button is built before
        /// the grid is touched, so a damaged file leaves the current board exactly as it was.
        /// </summary>
        private bool LoadBoardFile(string path)
        {
            var name = Path.GetFileName(path);
            var built = new List<SoundBoardButton>();
            try
            {
                if (JsonNode.Parse(File.ReadAllText(path)) is not JsonArray arr)
                    throw new System.Text.Json.JsonException("The file doesn't contain a list of buttons.");
                foreach (var node in arr)
                    if (node is JsonObject obj)
                        built.Add(new SoundBoardButton(this, obj));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                           or System.Text.Json.JsonException
                                           or InvalidOperationException or FormatException or OverflowException)
            {
                foreach (var btn in built) btn.Cleanup();
                MessageBox.Show($"Could not load '{name}':\n{ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Old buttons must go first: their hotkeys would otherwise block the new ones from registering.
            ClearGrid();
            foreach (var btn in built)
                SoundBoardGrid.Items.Insert(SoundBoardGrid.Items.Count - 1, btn);
            foreach (var btn in built)
                btn.ActivateHotkey();
            ApplySearch(SearchBox.Text);

            SoundBoardTitle.Text = Path.GetFileNameWithoutExtension(path);
            _currentBoardPath = path;
            _savedSnapshot = BoardSnapshot();
            WarnAboutUnregisteredHotkeys();
            return true;
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
            Directory.CreateDirectory(AppPaths.BoardsDirectory);
            Process.Start(new ProcessStartInfo { FileName = AppPaths.BoardsDirectory, UseShellExecute = true });
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
            UpdateThemeChecks(theme);
            ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, true);
            _settings.Theme = theme.ToString();
        }

        private void UpdateThemeChecks(ApplicationTheme theme)
        {
            DayMenuItem.IsChecked = theme == ApplicationTheme.Light;
            NightMenuItem.IsChecked = theme == ApplicationTheme.Dark;
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
