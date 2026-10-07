using MySoundBoard.Controls.Dialogs;
using MySoundBoard.Managers;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MySoundBoard.Controls
{
    public partial class SoundBoardButton : UserControl
    {
        #region State

        private bool _isPlaying;

        private bool _loopSound;
        private bool _playThroughHeadphones;
        private bool _fadeEnabled = false;
        private string _soundFile = string.Empty;
        private float _buttonVolume = 1.0f;
        private string _buttonColor = string.Empty;

        private double _fadeInSeconds;
        private double _fadeOutSeconds;
        private double _autoStopSeconds;

        private int _hotkeyId = -1;
        private uint _hotkeyModifiers;
        private uint _hotkeyVirtualKey;
        private string _hotkeyDisplay = string.Empty;

        private AudioPlayer? _audioPlayer;
        private AudioPlayer? _headphonePlayer;
        private SymbolRegular _customPlayIcon = SymbolRegular.Play48;
        private DispatcherTimer? _progressTimer;

        private Point _dragStartPoint;
        private bool _isCleanedUp;
        private bool _fadeOutStarted;
        private readonly Stopwatch _playStopwatch = new();

        public string Title { get; set; } = string.Empty;
        public double CurrentTrackLength { get; set; }

        /// <summary>The board has a hotkey for this button, but it couldn't be registered (e.g. another app owns it).</summary>
        public bool HasUnregisteredHotkey => _hotkeyVirtualKey > 0 && _hotkeyId < 0;
        public string HotkeyDisplay => _hotkeyDisplay;

        #endregion

        public SoundBoardButton()
        {
            InitializeComponent();
            Initialize();
        }

        public SoundBoardButton(JsonObject jObj)
        {
            InitializeComponent();
            Initialize();
            Deserialized(jObj);
        }

        private void Initialize()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _progressTimer.Tick += ProgressTimer_Tick;
        }

        private float EffectiveVolume => (MainWindow.Instance?.Volume ?? 1f) * _buttonVolume;

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

        // ── Playback ──────────────────────────────────────────────────────────

        // Clicking (or the hotkey) toggles between starting from the beginning and stopping.
        private void PlayButton_Click(object sender, RoutedEventArgs e) => TogglePlayback();

        private void TogglePlayback()
        {
            if (_isPlaying)
                StopPlayback();
            else
                StartPlayback();
        }

        private void StartPlayback()
        {
            if (string.IsNullOrEmpty(_soundFile) || _isPlaying) return;
            try
            {
                // A looping restart or an earlier failed start can leave old players behind.
                ReleasePlayers();

                float effectiveVol = EffectiveVolume;
                var outputDevice = MainWindow.GetSelectedOutputDevice();
                var headphoneDevice = MainWindow.GetSelectedHeadphoneDevice();
                bool useDualOutput = _playThroughHeadphones
                                     && headphoneDevice != null
                                     && headphoneDevice.Guid != outputDevice?.Guid;

                _audioPlayer = new AudioPlayer(_soundFile, effectiveVol, outputDevice!, _loopSound);
                _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                CurrentTrackLength = _audioPlayer.GetLengthInSeconds();

                if (useDualOutput)
                    _headphonePlayer = new AudioPlayer(_soundFile, effectiveVol, headphoneDevice!, _loopSound);

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
                System.Windows.MessageBox.Show(
                    $"Could not play '{System.IO.Path.GetFileName(_soundFile)}':\n{ex.Message}",
                    "Playback Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>Stops and resets to the beginning; the next play starts the sound over.</summary>
        public void StopPlayback()
        {
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
            double position = _audioPlayer.GetPositionInSeconds();

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
            _progressTimer?.Start();
        }

        private void StopProgressTimer()
        {
            _progressTimer?.Stop();
            PlaybackFillRect.Width = 0;
        }

        // ── Volume slider ─────────────────────────────────────────────────────

        private void ButtonVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _buttonVolume = (float)e.NewValue;
            ButtonVolumeSlider.ToolTip = $"Button volume: {(int)(e.NewValue * 100)}%";
            UpdateVolume();
        }

        // ── Edit / Icon / Loop / Headphone / Delete ───────────────────────────

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Filter = "Audio files (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
            {
                title.Text = openFileDialog.SafeFileName;
                Title = title.Text;
                _soundFile = openFileDialog.FileName;
                UpdateMissingFileState();
            }
        }

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
            MainWindow.RemoveButton(this);
        }

        public void Cleanup()
        {
            _isCleanedUp = true;
            CancelTimers();
            _progressTimer?.Stop();
            if (_progressTimer != null)
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

        private void SetHotkeyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new HotkeyInputWindow { Owner = System.Windows.Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;

            UnregisterHotkey();
            _hotkeyModifiers = win.CapturedModifiers;
            _hotkeyVirtualKey = win.CapturedKey;
            _hotkeyDisplay = win.HotkeyText;
            RegisterHotkey();

            if (_hotkeyId < 0)
            {
                ClearHotkey();
                System.Windows.MessageBox.Show(
                    $"The hotkey '{win.HotkeyText}' is already in use by another application or button.",
                    "Hotkey Unavailable", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        // Registers the stored hotkey. On failure the hotkey stays assigned (so it is still
        // saved with the board) and the badge shows it struck through.
        private void RegisterHotkey()
        {
            if (_hotkeyVirtualKey > 0)
            {
                _hotkeyId = MainWindow.Instance?.HotkeyManager?.Register(
                    _hotkeyModifiers, _hotkeyVirtualKey,
                    () => Dispatcher.Invoke(TogglePlayback)) ?? -1;
            }
            UpdateHotkeyBadge();
        }

        private void UnregisterHotkey()
        {
            if (_hotkeyId < 0) return;
            MainWindow.Instance?.HotkeyManager?.Unregister(_hotkeyId);
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
            var copy = new SoundBoardButton(jObj);
            MainWindow.Instance?.AddButtonAfter(this, copy);
        }

        // Dim the play button and explain why when the saved sound file has moved or been deleted.
        private void UpdateMissingFileState()
        {
            bool missing = !string.IsNullOrEmpty(_soundFile) && !System.IO.File.Exists(_soundFile);
            PlayButton.Opacity = missing ? 0.4 : 1.0;
            PlayButton.ToolTip = missing ? $"Sound file not found: {_soundFile}" : null;
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

        // ── Drag-and-drop ─────────────────────────────────────────────────────

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

        private void RootBorder_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(SoundBoardButton)) && e.Data.GetData(typeof(SoundBoardButton)) != this)
                RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        }

        private void RootBorder_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, IdleBorderKey);
        }

        private void RootBorder_Drop(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, IdleBorderKey);
            if (e.Data.GetDataPresent(typeof(SoundBoardButton)))
            {
                var source = (SoundBoardButton)e.Data.GetData(typeof(SoundBoardButton));
                if (source != this)
                    MainWindow.Instance?.MoveButton(source, this);
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

        private void Deserialized(JsonObject jObj)
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
            UpdateMissingFileState();

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

            if (jObj.TryGetPropertyValue("HotkeyModifiers", out v) && v != null)
                _hotkeyModifiers = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyVirtualKey", out v) && v != null)
                _hotkeyVirtualKey = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyDisplay", out v) && v != null)
                _hotkeyDisplay = v.GetValue<string>();
            RegisterHotkey();
        }

        private void title_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (e.Source is TextBox textBox)
                Title = textBox.Text;
        }
    }
}
