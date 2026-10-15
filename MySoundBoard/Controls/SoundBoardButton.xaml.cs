using MySoundBoard.Controls.Dialogs;
using MySoundBoard.Managers;
using MySoundBoard.Utilities;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using TextBox = Wpf.Ui.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MySoundBoard.Controls
{
    public partial class SoundBoardButton : UserControl
    {
        #region State

        private readonly ISoundBoardHost _host;

        private bool _isPlaying;
        private PlayMode _playMode = PlayMode.Toggle;

        private bool _loopSound;
        private bool _playThroughHeadphones;
        private bool _fadeEnabled = false;
        private string _soundFile = string.Empty;
        private float _buttonVolume = 1.0f;
        private string _buttonColor = string.Empty;

        private double _fadeInSeconds;
        private double _fadeOutSeconds;
        private double _autoStopSeconds;
        private double _trimStartSeconds;
        private double _trimEndSeconds;

        private int _hotkeyId = -1;
        private uint _hotkeyModifiers;
        private uint _hotkeyVirtualKey;
        private string _hotkeyDisplay = string.Empty;

        private AudioPlayer? _audioPlayer;
        private AudioPlayer? _headphonePlayer;
        private SymbolRegular _customPlayIcon = SymbolRegular.Play48;
        private readonly DispatcherTimer _progressTimer;
        // Hold mode via hotkey: WM_HOTKEY only reports the press, so poll for the release.
        private readonly DispatcherTimer _holdReleasePoll;

        private Point _dragStartPoint;
        private bool _isCleanedUp;
        private bool _fadeOutStarted;
        private readonly System.Diagnostics.Stopwatch _playStopwatch = new();

        public string Title { get; set; } = string.Empty;
        public double CurrentTrackLength { get; set; }

        /// <summary>The board has a hotkey for this button, but it couldn't be registered (e.g. another app owns it).</summary>
        public bool HasUnregisteredHotkey => _hotkeyVirtualKey > 0 && _hotkeyId < 0;
        public string HotkeyDisplay => _hotkeyDisplay;

        private bool HasSoundFile => !string.IsNullOrEmpty(_soundFile);
        private bool IsSoundMissing => HasSoundFile && !File.Exists(_soundFile);

        #endregion

        public SoundBoardButton(ISoundBoardHost host)
        {
            _host = host;
            InitializeComponent();
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _holdReleasePoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _holdReleasePoll.Tick += HoldReleasePoll_Tick;
            UpdatePlayButtonHint();
        }

        /// <summary>
        /// Restores a saved button. The hotkey is not registered until <see cref="ActivateHotkey"/>,
        /// so a board can be fully built (and discarded on error) before it takes any global keys.
        /// </summary>
        public SoundBoardButton(ISoundBoardHost host, JsonObject jObj) : this(host)
        {
            Deserialize(jObj);
        }

        public static SoundBoardButton ForFile(ISoundBoardHost host, string path)
        {
            var button = new SoundBoardButton(host);
            button.AssignSoundFile(path);
            return button;
        }

        private float EffectiveVolume => _host.Volume * _buttonVolume;

        // ── Toggle styling ────────────────────────────────────────────────────
        // Resource references follow theme and accent changes on their own; clearing the
        // local value hands the button back to its themed default style.

        private const string IdleBorderKey = "ControlStrokeColorDefaultBrush";

        private static void SetToggleStyle(Button button, bool active)
        {
            if (active)
            {
                button.SetResourceReference(BackgroundProperty, "SystemAccentColorPrimaryBrush");
                button.SetResourceReference(Button.MouseOverBackgroundProperty, "SystemAccentColorSecondaryBrush");
            }
            else
            {
                button.ClearValue(BackgroundProperty);
                button.ClearValue(Button.MouseOverBackgroundProperty);
            }
        }

        private void ApplyToggleStyles()
        {
            SetToggleStyle(LoopButton, _loopSound);
            SetToggleStyle(HeadPhoneButton, _playThroughHeadphones);
            SetToggleStyle(FadeButton, _fadeEnabled);
            FadeButton.IsEnabled = !_loopSound;
        }

        // ── Play button input ─────────────────────────────────────────────────

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSoundFile) return;
            if (IsSoundMissing)
            {
                PromptToLocateMissingFile();
                return;
            }
            switch (_playMode)
            {
                case PlayMode.Toggle: TogglePlayback(); break;
                case PlayMode.Restart: RestartPlayback(); break;
                // Hold is driven by the mouse down/up handlers below.
            }
        }

        private void PlayButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Not marked handled: the button still needs to capture the mouse so the release reaches us.
            if (_playMode == PlayMode.Hold && HasSoundFile && !IsSoundMissing)
                StartPlayback();
        }

        private void PlayButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_playMode == PlayMode.Hold) StopPlayback();
        }

        private void PlayButton_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_playMode == PlayMode.Hold && !_holdReleasePoll.IsEnabled) StopPlayback();
        }

        private void OnHotkeyPressed()
        {
            // No dialogs from a hotkey: the user is probably in another app.
            if (!HasSoundFile || IsSoundMissing) return;
            switch (_playMode)
            {
                case PlayMode.Toggle:
                    TogglePlayback();
                    break;
                case PlayMode.Restart:
                    RestartPlayback();
                    break;
                case PlayMode.Hold:
                    if (_isPlaying) return;
                    StartPlayback();
                    if (_isPlaying) _holdReleasePoll.Start();
                    break;
            }
        }

        private void HoldReleasePoll_Tick(object? sender, EventArgs e)
        {
            if (_isPlaying && HotkeyManager.IsKeyDown(_hotkeyVirtualKey)) return;
            _holdReleasePoll.Stop();
            StopPlayback();
        }

        // ── Playback ──────────────────────────────────────────────────────────

        private void TogglePlayback()
        {
            if (_isPlaying)
                StopPlayback();
            else
                StartPlayback();
        }

        private void RestartPlayback()
        {
            if (_isPlaying)
            {
                // Released players are detached first, so their stop can't bounce back into a loop restart.
                CancelTimers();
                ReleasePlayers();
                _isPlaying = false;
            }
            StartPlayback();
        }

        private void StartPlayback()
        {
            if (!HasSoundFile || _isPlaying) return;
            try
            {
                // A looping restart or an earlier failed start can leave old players behind.
                ReleasePlayers();

                float effectiveVol = EffectiveVolume;
                var outputDevice = _host.PrimaryDevice;
                var headphoneDevice = _host.SecondaryDevice;
                bool useDualOutput = _playThroughHeadphones
                                     && headphoneDevice != null
                                     && headphoneDevice.Guid != outputDevice?.Guid;

                _audioPlayer = CreatePlayer(outputDevice!, effectiveVol);
                _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                CurrentTrackLength = _audioPlayer.GetRegionLengthInSeconds();

                if (useDualOutput)
                    _headphonePlayer = CreatePlayer(headphoneDevice!, effectiveVol);

                if (_fadeInSeconds > 0 && _fadeEnabled && !_loopSound)
                {
                    _audioPlayer.BeginFadeIn(_fadeInSeconds * 1000);
                    _headphonePlayer?.BeginFadeIn(_fadeInSeconds * 1000);
                }
                _audioPlayer.Play();
                _headphonePlayer?.Play();

                _isPlaying = true;
                PlayButton.Icon = new SymbolIcon { Symbol = SymbolRegular.Stop24 };
                ResetAndStartProgressTimer();
            }
            catch (Exception ex)
            {
                ReleasePlayers();
                _isPlaying = false;
                MessageBox.Show(
                    $"Could not play '{Path.GetFileName(_soundFile)}':\n{ex.Message}",
                    "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private AudioPlayer CreatePlayer(NAudio.Wave.DirectSoundDeviceInfo device, float volume)
            => new(_soundFile, volume, device, _loopSound, _trimStartSeconds, _trimEndSeconds);

        /// <summary>Stops and resets to the beginning; the next play starts the sound over.</summary>
        public void StopPlayback()
        {
            _holdReleasePoll.Stop();
            if (!_isPlaying) return;
            CancelTimers();
            _audioPlayer?.Stop();
            _headphonePlayer?.Stop();
        }

        public void UpdateVolume()
        {
            _audioPlayer?.SetVolume(EffectiveVolume);
            _headphonePlayer?.SetVolume(EffectiveVolume);
        }

        // ── Timers ────────────────────────────────────────────────────────────
        // Fade-out and auto-stop are driven from the progress tick, so one timer covers
        // the progress bar, the fade-out point and the auto-stop limit.

        private void CancelTimers()
        {
            _playStopwatch.Reset();
            _fadeOutStarted = false;
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioPlayer == null) return;

            if (_autoStopSeconds > 0 && _playStopwatch.Elapsed.TotalSeconds >= _autoStopSeconds)
            {
                StopPlayback();
                return;
            }

            if (CurrentTrackLength <= 0) return;
            double position = _audioPlayer.GetRegionPositionInSeconds();

            if (!_fadeOutStarted && _fadeEnabled && !_loopSound && _fadeOutSeconds > 0
                && position >= CurrentTrackLength - _fadeOutSeconds)
            {
                _fadeOutStarted = true;
                _audioPlayer.BeginFadeOut(_fadeOutSeconds * 1000);
                _headphonePlayer?.BeginFadeOut(_fadeOutSeconds * 1000);
            }

            double progress = Math.Clamp(position / CurrentTrackLength, 0, 1);
            PlaybackFillRect.Width = progress * PlayButton.ActualWidth;
        }

        private void ResetAndStartProgressTimer()
        {
            PlaybackFillRect.Width = 0;
            _fadeOutStarted = false;
            _playStopwatch.Restart();
            _progressTimer.Start();
        }

        private void StopProgressTimer()
        {
            _progressTimer.Stop();
            PlaybackFillRect.Width = 0;
        }

        // ── Volume slider ─────────────────────────────────────────────────────

        private void ButtonVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _buttonVolume = (float)e.NewValue;
            ButtonVolumeSlider.ToolTip = $"Button volume: {(int)(e.NewValue * 100)}%";
            UpdateVolume();
        }

        // ── Sound file ────────────────────────────────────────────────────────

        private void AssignSoundFile(string path)
        {
            _soundFile = path;
            title.Text = Path.GetFileName(path);
            Title = title.Text;
            UpdateMissingFileState();
        }

        // Picking several files fills this button with the first and adds the rest right after it.
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = (HasSoundFile ? AudioFiles.NearestExistingDirectory(_soundFile) : null)
                                   ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Filter = AudioFiles.DialogFilter,
                FilterIndex = 1,
                RestoreDirectory = true,
                Multiselect = true
            };
            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0) return;

            StopPlayback();
            AssignSoundFile(dialog.FileNames[0]);
            if (dialog.FileNames.Length > 1)
                _host.AddSoundFiles(dialog.FileNames.Skip(1), this);
        }

        private void PromptToLocateMissingFile()
        {
            var answer = MessageBox.Show(
                $"'{Path.GetFileName(_soundFile)}' can't be found at:\n{_soundFile}\n\nWould you like to locate it?",
                "Sound File Missing", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Yes) LocateSoundFile();
        }

        private void LocateMenuItem_Click(object sender, RoutedEventArgs e) => LocateSoundFile();

        // Unlike Edit, keeps the button's name and settings; only the file path changes.
        private void LocateSoundFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Locate Sound File",
                Filter = AudioFiles.DialogFilter,
                InitialDirectory = (HasSoundFile ? AudioFiles.NearestExistingDirectory(_soundFile) : null)
                                   ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                FileName = HasSoundFile ? Path.GetFileName(_soundFile) : string.Empty
            };
            if (dialog.ShowDialog() != true) return;

            bool wasMissing = IsSoundMissing;
            var oldDirectory = HasSoundFile ? Path.GetDirectoryName(_soundFile) : null;
            StopPlayback();
            _soundFile = dialog.FileName;
            if (string.IsNullOrEmpty(Title))
            {
                title.Text = Path.GetFileName(_soundFile);
                Title = title.Text;
            }
            UpdateMissingFileState();

            // A whole folder of sounds usually moves at once, so fix its neighbours too.
            var newDirectory = Path.GetDirectoryName(_soundFile);
            if (wasMissing && !string.IsNullOrEmpty(oldDirectory) && !string.IsNullOrEmpty(newDirectory)
                && !string.Equals(oldDirectory, newDirectory, StringComparison.OrdinalIgnoreCase))
            {
                int relinked = _host.RelinkMissingSounds(oldDirectory, newDirectory);
                if (relinked > 0)
                    MessageBox.Show($"Also found {relinked} other missing sound(s) in that folder and updated them.",
                        "Sounds Relinked", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>If this button's file is missing from <paramref name="oldDirectory"/> but exists under the same name in <paramref name="newDirectory"/>, switch to it.</summary>
        public bool TryRelink(string oldDirectory, string newDirectory)
        {
            if (!IsSoundMissing) return false;
            if (!string.Equals(Path.GetDirectoryName(_soundFile), oldDirectory, StringComparison.OrdinalIgnoreCase)) return false;
            var candidate = Path.Combine(newDirectory, Path.GetFileName(_soundFile));
            if (!File.Exists(candidate)) return false;
            _soundFile = candidate;
            UpdateMissingFileState();
            return true;
        }

        /// <summary>Re-checks whether the sound file exists, e.g. after the window regains focus.</summary>
        public void RefreshMissingFileState() => UpdateMissingFileState();

        // Dim the play button and explain why when the saved sound file has moved or been deleted.
        private void UpdateMissingFileState()
        {
            PlayButton.Opacity = IsSoundMissing ? 0.4 : 1.0;
            UpdatePlayButtonHint();
        }

        private void UpdatePlayButtonHint()
        {
            string hint;
            if (!HasSoundFile)
                hint = "Choose a sound with the pencil button";
            else if (IsSoundMissing)
                hint = $"Sound file not found: {_soundFile}\nClick to locate it.";
            else
            {
                hint = _playMode switch
                {
                    PlayMode.Restart => "Click to play from the start (restarts if already playing)",
                    PlayMode.Hold => "Hold to play, release to stop",
                    _ => "Click to play, click again to stop",
                };
                if (_trimStartSeconds > 0 || _trimEndSeconds > 0)
                    hint += _trimEndSeconds > 0
                        ? $"\nTrimmed to {_trimStartSeconds:F1}s – {_trimEndSeconds:F1}s"
                        : $"\nTrimmed to start at {_trimStartSeconds:F1}s";
            }
            PlayButton.ToolTip = hint;
        }

        // ── Icon / Loop / Headphone / Delete ──────────────────────────────────

        private void IconEditButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new IconPickerDialog(
                _customPlayIcon,
                preview => Dispatcher.Invoke(() => PlayButton.Icon = new SymbolIcon { Symbol = preview }));
            dialog.Owner = System.Windows.Window.GetWindow(this);
            if (dialog.ShowDialog() == true && dialog.SelectedSymbol.HasValue)
                _customPlayIcon = dialog.SelectedSymbol.Value;
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            _loopSound = !_loopSound;
            if (_audioPlayer != null) _audioPlayer.Loop = _loopSound;
            if (_headphonePlayer != null) _headphonePlayer.Loop = _loopSound;
            ApplyToggleStyles();
        }

        private void FadeButton_Click(object sender, RoutedEventArgs e)
        {
            _fadeEnabled = !_fadeEnabled;
            ApplyToggleStyles();
        }

        private void HeadphoneButton_Click(object sender, RoutedEventArgs e)
        {
            _playThroughHeadphones = !_playThroughHeadphones;
            ApplyToggleStyles();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            _host.RemoveButton(this);
        }

        public void Cleanup()
        {
            _isCleanedUp = true;
            CancelTimers();
            _holdReleasePoll.Stop();
            _progressTimer.Stop();
            _progressTimer.Tick -= ProgressTimer_Tick;
            UnregisterHotkey();
            ReleasePlayers();
        }

        // Detach events before disposing so a late PlaybackStopped can't restart a looping sound.
        private void ReleasePlayers()
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.PlaybackStopped -= _audioPlayer_PlaybackStopped;
                _audioPlayer.Dispose();
                _audioPlayer = null;
            }
            _headphonePlayer?.Dispose();
            _headphonePlayer = null;
        }

        // ── Context menu ──────────────────────────────────────────────────────

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ToggleModeMenuItem.IsChecked = _playMode == PlayMode.Toggle;
            RestartModeMenuItem.IsChecked = _playMode == PlayMode.Restart;
            HoldModeMenuItem.IsChecked = _playMode == PlayMode.Hold;
            ClearHotkeyMenuItem.IsEnabled = _hotkeyVirtualKey > 0;
        }

        private void PlayModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem { Tag: string tag } && Enum.TryParse<PlayMode>(tag, out var mode))
            {
                StopPlayback();
                _playMode = mode;
                UpdatePlayButtonHint();
            }
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            title.Focus();
            title.SelectAll();
        }

        private void SetColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.ColorDialog();
            if (TryParseColor(_buttonColor, out var current))
                dlg.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);
            if (dlg.ShowDialog() == DialogResult.OK)
                ApplyButtonColor($"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}");
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(value)) return false;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // Unparseable colours (hand-edited or corrupt boards) fall back to the theme background.
        private void ApplyButtonColor(string value)
        {
            if (TryParseColor(value, out var color))
            {
                _buttonColor = value;
                RootBorder.Background = new SolidColorBrush(color);
            }
            else
            {
                _buttonColor = string.Empty;
                RootBorder.ClearValue(System.Windows.Controls.Border.BackgroundProperty);
            }
        }

        private void TrimMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!HasSoundFile || IsSoundMissing)
            {
                MessageBox.Show("Choose a sound file for this button before trimming it.",
                    "Trim Sound", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double duration;
            try
            {
                duration = AudioPlayer.GetFileDurationSeconds(_soundFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read '{Path.GetFileName(_soundFile)}':\n{ex.Message}",
                    "Trim Sound", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var win = new TrimWindow(duration, _trimStartSeconds, _trimEndSeconds)
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
            {
                _trimStartSeconds = win.TrimStart;
                _trimEndSeconds = win.TrimEnd;
                UpdatePlayButtonHint();
            }
        }

        // ── Hotkey ────────────────────────────────────────────────────────────

        private void SetHotkeyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new HotkeyInputWindow { Owner = System.Windows.Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            UnregisterHotkey();
            _hotkeyModifiers = win.CapturedModifiers;
            _hotkeyVirtualKey = win.CapturedKey;
            _hotkeyDisplay = win.HotkeyText;
            ActivateHotkey();

            if (_hotkeyId < 0)
            {
                ClearHotkey();
                MessageBox.Show(
                    $"The hotkey '{win.HotkeyText}' is already in use by another application or button.",
                    "Hotkey Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearHotkeyMenuItem_Click(object sender, RoutedEventArgs e) => ClearHotkey();

        /// <summary>
        /// Registers the stored hotkey if it isn't already. On failure the hotkey stays assigned
        /// (so it is still saved with the board) and the badge shows it struck through.
        /// </summary>
        public void ActivateHotkey()
        {
            if (_hotkeyVirtualKey > 0 && _hotkeyId < 0)
            {
                _hotkeyId = _host.Hotkeys?.Register(
                    _hotkeyModifiers, _hotkeyVirtualKey,
                    () => Dispatcher.Invoke(OnHotkeyPressed)) ?? -1;
            }
            UpdateHotkeyBadge();
        }

        private void UnregisterHotkey()
        {
            if (_hotkeyId < 0) return;
            _host.Hotkeys?.Unregister(_hotkeyId);
            _hotkeyId = -1;
        }

        private void ClearHotkey()
        {
            UnregisterHotkey();
            _hotkeyModifiers = 0;
            _hotkeyVirtualKey = 0;
            _hotkeyDisplay = string.Empty;
            UpdateHotkeyBadge();
        }

        private void UpdateHotkeyBadge()
        {
            if (string.IsNullOrEmpty(_hotkeyDisplay))
            {
                HotkeyBadge.Visibility = Visibility.Collapsed;
                return;
            }
            HotkeyBadgeText.Text = _hotkeyDisplay;
            HotkeyBadgeText.TextDecorations = HasUnregisteredHotkey ? TextDecorations.Strikethrough : null;
            HotkeyBadge.Visibility = Visibility.Visible;
        }

        // ── Other dialogs ─────────────────────────────────────────────────────

        private void FadeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new FadeSettingsWindow(_fadeInSeconds, _fadeOutSeconds)
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
            {
                _fadeInSeconds = win.FadeIn;
                _fadeOutSeconds = win.FadeOut;
            }
        }

        private void AutoStopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new AutoStopWindow(_autoStopSeconds)
            {
                Owner = System.Windows.Window.GetWindow(this)
            };
            if (win.ShowDialog() == true)
                _autoStopSeconds = win.AutoStopSeconds;
        }

        private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var jObj = Serialize();
            // Hotkeys must be unique — strip from duplicate
            jObj.Remove("HotkeyModifiers");
            jObj.Remove("HotkeyVirtualKey");
            jObj.Remove("HotkeyDisplay");
            _host.InsertButtonAfter(this, new SoundBoardButton(_host, jObj));
        }

        // ── Drag-and-drop ─────────────────────────────────────────────────────
        // Dragging a tile reorders it; dropping audio files or folders from Explorer
        // onto a tile adds them as new buttons right after it.

        private void RootBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void RootBorder_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (Mouse.Captured != null && !ReferenceEquals(Mouse.Captured, RootBorder)) return;
            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                Opacity = 0.5;
                DragDrop.DoDragDrop(this, this, System.Windows.DragDropEffects.Move);
                Opacity = 1.0;
            }
        }

        private bool IsAcceptedDrop(System.Windows.DragEventArgs e)
            => e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
               || (e.Data.GetDataPresent(typeof(SoundBoardButton)) && e.Data.GetData(typeof(SoundBoardButton)) != this);

        private void RootBorder_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (IsAcceptedDrop(e))
                RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        }

        private void RootBorder_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy
                : IsAcceptedDrop(e) ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void RootBorder_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, IdleBorderKey);
        }

        private void RootBorder_Drop(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, IdleBorderKey);
            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
            {
                _host.AddSoundFiles(files, this);
                e.Handled = true;
            }
            else if (e.Data.GetData(typeof(SoundBoardButton)) is SoundBoardButton source && source != this)
            {
                _host.MoveButton(source, this);
                e.Handled = true;
            }
        }

        // ── Playback events ───────────────────────────────────────────────────

        private void _audioPlayer_PlaybackStopped()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isCleanedUp) return;
                CancelTimers();
                _isPlaying = false;
                PlayButton.Icon = new SymbolIcon { Symbol = _customPlayIcon };
                // Looping is normally gapless inside the player; this catches loop being
                // switched on just as the track reached its end.
                if (_audioPlayer?.PlaybackStopType == AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && _loopSound)
                    StartPlayback();
                else
                    StopProgressTimer();
            });
        }

        // ── Serialization ─────────────────────────────────────────────────────

        public JsonObject Serialize()
        {
            var jObj = new JsonObject
            {
                ["LoopSound"] = _loopSound,
                ["PlayThroughHeadphones"] = _playThroughHeadphones,
                ["soundFile"] = _soundFile,
                ["Title"] = Title,
                ["CustomPlayIcon"] = _customPlayIcon.ToString(),
                ["ButtonVolume"] = _buttonVolume,
                ["ButtonColor"] = _buttonColor,
                ["FadeInSeconds"] = _fadeInSeconds,
                ["FadeOutSeconds"] = _fadeOutSeconds,
                ["FadeEnabled"] = _fadeEnabled,
                ["AutoStopSeconds"] = _autoStopSeconds,
                ["PlayMode"] = _playMode.ToString(),
                ["TrimStartSeconds"] = _trimStartSeconds,
                ["TrimEndSeconds"] = _trimEndSeconds,
            };
            // Saved even when registration failed, so a temporarily taken hotkey isn't lost.
            if (_hotkeyVirtualKey > 0)
            {
                jObj.Add("HotkeyModifiers", _hotkeyModifiers);
                jObj.Add("HotkeyVirtualKey", _hotkeyVirtualKey);
                jObj.Add("HotkeyDisplay", _hotkeyDisplay);
            }
            return jObj;
        }

        private void Deserialize(JsonObject jObj)
        {
            JsonNode? v;

            if (jObj.TryGetPropertyValue("LoopSound", out v) && v != null)
                _loopSound = v.GetValue<bool>();
            if (jObj.TryGetPropertyValue("PlayThroughHeadphones", out v) && v != null)
                _playThroughHeadphones = v.GetValue<bool>();
            if (jObj.TryGetPropertyValue("FadeEnabled", out v) && v != null)
                _fadeEnabled = v.GetValue<bool>();
            ApplyToggleStyles();

            if (jObj.TryGetPropertyValue("soundFile", out v) && v != null)
                _soundFile = v.GetValue<string>() ?? string.Empty;

            if (jObj.TryGetPropertyValue("Title", out v) && v != null)
            {
                Title = v.GetValue<string>();
                title.Text = Title;
            }
            if (jObj.TryGetPropertyValue("CustomPlayIcon", out v) && v != null
                && Enum.TryParse<SymbolRegular>(v.GetValue<string>(), out var icon))
            {
                _customPlayIcon = icon;
                PlayButton.Icon = new SymbolIcon { Symbol = _customPlayIcon };
            }
            if (jObj.TryGetPropertyValue("ButtonVolume", out v) && v != null)
            {
                _buttonVolume = v.GetValue<float>();
                ButtonVolumeSlider.Value = _buttonVolume;
            }
            if (jObj.TryGetPropertyValue("ButtonColor", out v) && v != null)
                ApplyButtonColor(v.GetValue<string>() ?? string.Empty);
            if (jObj.TryGetPropertyValue("FadeInSeconds", out v) && v != null)
                _fadeInSeconds = v.GetValue<double>();
            if (jObj.TryGetPropertyValue("FadeOutSeconds", out v) && v != null)
                _fadeOutSeconds = v.GetValue<double>();
            if (jObj.TryGetPropertyValue("AutoStopSeconds", out v) && v != null)
                _autoStopSeconds = v.GetValue<double>();
            if (jObj.TryGetPropertyValue("PlayMode", out v) && v != null
                && Enum.TryParse<PlayMode>(v.GetValue<string>(), out var mode))
                _playMode = mode;
            if (jObj.TryGetPropertyValue("TrimStartSeconds", out v) && v != null)
                _trimStartSeconds = Math.Max(0, v.GetValue<double>());
            if (jObj.TryGetPropertyValue("TrimEndSeconds", out v) && v != null)
                _trimEndSeconds = Math.Max(0, v.GetValue<double>());

            if (jObj.TryGetPropertyValue("HotkeyModifiers", out v) && v != null)
                _hotkeyModifiers = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyVirtualKey", out v) && v != null)
                _hotkeyVirtualKey = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyDisplay", out v) && v != null)
                _hotkeyDisplay = v.GetValue<string>();

            UpdateMissingFileState();
            UpdateHotkeyBadge();
        }

        private void title_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (e.Source is TextBox textBox)
                Title = textBox.Text;
        }
    }
}
