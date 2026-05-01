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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = Wpf.Ui.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MySoundBoard.Controls
{
    public partial class SoundBoardButton : UserControl
    {
        #region State

        private enum PlaybackState { Playing, Stopped, Paused }
        private PlaybackState _playbackState;

        private bool LoopSound;
        private bool PlayThroughHeadphones;
        private bool _fadeEnabled = true;
        private string soundFile = string.Empty;
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
        private DispatcherTimer _progressTimer;
        private DispatcherTimer? _fadeOutTimer;
        private DispatcherTimer? _autoStopTimer;

        private Brush? _unselectedBrush;
        private Brush? _unselectedBrushHover;

        private Point _dragStartPoint;

        public string Title { get; set; } = string.Empty;
        public double CurrentTrackLenght { get; set; }

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
            _unselectedBrush = LoopButton.Background;
            _unselectedBrushHover = LoopButton.MouseOverBorderBrush;
            _playbackState = PlaybackState.Stopped;

            HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
            HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;

            FadeButton.Background = _fadeEnabled ? Brushes.Blue : _unselectedBrush;
            FadeButton.MouseOverBackground = _fadeEnabled ? Brushes.DarkBlue : _unselectedBrushHover;

            MainWindow.Instance.ThemeChanged += ThemeChanged_Event;

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _progressTimer.Tick += ProgressTimer_Tick;
        }

        private void ThemeChanged_Event(object? sender, RoutedEventArgs e)
        {
            var tmp = new Button();
            _unselectedBrush = tmp.Background;
            _unselectedBrushHover = tmp.MouseOverBorderBrush;
        }

        // ── Playback ──────────────────────────────────────────────────────────

        private void PlayButton_Click(object sender, RoutedEventArgs e) => StartPlaying();

        private void StartPlaying()
        {
            if (string.IsNullOrEmpty(soundFile)) return;

            if (_playbackState == PlaybackState.Paused)
            {
                float effectiveVol = (MainWindow.Instance.Volume / 100f) * _buttonVolume;
                _audioPlayer?.TogglePlayPause(effectiveVol);
                _headphonePlayer?.TogglePlayPause(effectiveVol);
                return;
            }

            if (_playbackState == PlaybackState.Stopped)
            {
                try
                {
                    float effectiveVol = (MainWindow.Instance.Volume / 100f) * _buttonVolume;
                    var outputDevice = MainWindow.GetSelectedOutputDevice();
                    var headphoneDevice = MainWindow.GetSelectedHeadphoneDevice();
                    bool useDualOutput = PlayThroughHeadphones
                        && headphoneDevice != null
                        && headphoneDevice.Guid != outputDevice?.Guid;

                    _audioPlayer = new AudioPlayer(soundFile, effectiveVol, outputDevice!);
                    _audioPlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                    _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                    _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                    _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                    CurrentTrackLenght = _audioPlayer.GetLenghtInSeconds();

                    if (useDualOutput)
                    {
                        _headphonePlayer = new AudioPlayer(soundFile, effectiveVol, headphoneDevice!);
                        _headphonePlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                        _headphonePlayer.PlaybackStopped += _headphonePlayer_PlaybackStopped;
                    }

                    _audioPlayer.TogglePlayPause(effectiveVol);
                    if (_fadeInSeconds > 0 && _fadeEnabled && !LoopSound)
                    {
                        _audioPlayer.BeginFadeIn(_fadeInSeconds * 1000);
                        _headphonePlayer?.BeginFadeIn(_fadeInSeconds * 1000);
                    }
                    if (useDualOutput)
                        _headphonePlayer?.TogglePlayPause(effectiveVol);

                    PlayButton.Icon = new SymbolIcon { Symbol = SymbolRegular.Pause48 };
                    ResetAndStartProgressTimer();
                    StartFadeOutTimer();
                    StartAutoStopTimer();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Could not play '{System.IO.Path.GetFileName(soundFile)}':\n{ex.Message}",
                        "Playback Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            else
            {
                StopPlayback();
            }
        }

        public void StopPlayback()
        {
            if (_playbackState == PlaybackState.Stopped) return;
            CancelTimers();
            if (_audioPlayer != null)
            {
                _audioPlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _audioPlayer.Stop();
            }
            if (_headphonePlayer != null)
            {
                _headphonePlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _headphonePlayer.Stop();
            }
        }

        public void UpdateVolume(float globalVolume)
        {
            float effective = globalVolume * _buttonVolume;
            _audioPlayer?.SetVolume(effective);
            _headphonePlayer?.SetVolume(effective);
        }

        // ── Timers ────────────────────────────────────────────────────────────

        private void StartFadeOutTimer()
        {
            if (_fadeOutSeconds <= 0 || CurrentTrackLenght <= 0 || !_fadeEnabled || LoopSound) return;
            double delay = Math.Max(0, CurrentTrackLenght - _fadeOutSeconds);
            _fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            _fadeOutTimer.Tick += (s, e) =>
            {
                _fadeOutTimer!.Stop();
                _fadeOutTimer = null;
                if (_audioPlayer != null)
                {
                    _audioPlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                    _audioPlayer.BeginFadeOut(_fadeOutSeconds * 1000);
                }
                _headphonePlayer?.BeginFadeOut(_fadeOutSeconds * 1000);
            };
            _fadeOutTimer.Start();
        }

        private void StartAutoStopTimer()
        {
            if (_autoStopSeconds <= 0) return;
            _autoStopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_autoStopSeconds) };
            _autoStopTimer.Tick += (s, e) =>
            {
                _autoStopTimer!.Stop();
                _autoStopTimer = null;
                StopPlayback();
            };
            _autoStopTimer.Start();
        }

        private void CancelTimers()
        {
            _fadeOutTimer?.Stop();
            _fadeOutTimer = null;
            _autoStopTimer?.Stop();
            _autoStopTimer = null;
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_audioPlayer == null || CurrentTrackLenght <= 0) return;
            double progress = Math.Clamp(_audioPlayer.GetPositionInSeconds() / CurrentTrackLenght, 0, 1);
            PlaybackFillRect.Width = progress * PlayButton.ActualWidth;
        }

        private void ResetAndStartProgressTimer()
        {
            PlaybackFillRect.Width = 0;
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
            float globalVol = MainWindow.Instance?.Volume / 100f ?? 1f;
            _audioPlayer?.SetVolume(globalVol * _buttonVolume);
            _headphonePlayer?.SetVolume(globalVol * _buttonVolume);
        }

        // ── Edit / Icon / Loop / Headphone / Delete ───────────────────────────

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("EditButton Click");
            using var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            openFileDialog.Filter = "Audio files (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
            {
                title.Text = openFileDialog.SafeFileName;
                Title = title.Text;
                soundFile = openFileDialog.FileName;
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
            LoopSound = !LoopSound;
            LoopButton.Background = LoopSound ? Brushes.Blue : _unselectedBrush;
            LoopButton.MouseOverBackground = LoopSound ? Brushes.DarkBlue : _unselectedBrushHover;
            FadeButton.IsEnabled = !LoopSound;
        }

        private void FadeButton_Click(object sender, RoutedEventArgs e)
        {
            _fadeEnabled = !_fadeEnabled;
            FadeButton.Background = _fadeEnabled ? Brushes.Blue : _unselectedBrush;
            FadeButton.MouseOverBackground = _fadeEnabled ? Brushes.DarkBlue : _unselectedBrushHover;
        }

        private void HeadphoneButton_Click(object sender, RoutedEventArgs e)
        {
            PlayThroughHeadphones = !PlayThroughHeadphones;
            HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
            HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Cleanup();
            MainWindow.RemoveButton(this);
        }

        public void Cleanup()
        {
            CancelTimers();
            if (_hotkeyId >= 0)
                MainWindow.Instance?.HotkeyManager?.Unregister(_hotkeyId);
            _audioPlayer?.Dispose();
            _headphonePlayer?.Dispose();
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
            if (!string.IsNullOrEmpty(_buttonColor))
            {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_buttonColor);
                dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
            }
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _buttonColor = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                RootBorder.Background = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_buttonColor));
            }
        }

        private void SetHotkeyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new HotkeyInputWindow { Owner = System.Windows.Window.GetWindow(this) };
            if (win.ShowDialog() == true && win.WasAssigned)
            {
                if (_hotkeyId >= 0)
                    MainWindow.Instance?.HotkeyManager?.Unregister(_hotkeyId);

                _hotkeyModifiers = win.CapturedModifiers;
                _hotkeyVirtualKey = win.CapturedKey;
                _hotkeyDisplay = win.HotkeyText;
                _hotkeyId = MainWindow.Instance?.HotkeyManager?.Register(
                    _hotkeyModifiers, _hotkeyVirtualKey,
                    () => Dispatcher.Invoke(StartPlaying)) ?? -1;

                UpdateHotkeyBadge();
            }
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
            var copy = new SoundBoardButton(jObj);
            MainWindow.Instance?.AddButtonAfter(this, copy);
        }

        private void UpdateHotkeyBadge()
        {
            if (string.IsNullOrEmpty(_hotkeyDisplay))
            {
                HotkeyBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                HotkeyBadgeText.Text = _hotkeyDisplay;
                HotkeyBadge.Visibility = Visibility.Visible;
            }
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
                RootBorder.BorderBrush = Brushes.DodgerBlue;
        }

        private void RootBorder_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.BorderBrush = Brushes.LightGray;
        }

        private void RootBorder_Drop(object sender, System.Windows.DragEventArgs e)
        {
            RootBorder.BorderBrush = Brushes.LightGray;
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
                CancelTimers();
                _playbackState = PlaybackState.Stopped;
                PlayButton.Icon = new SymbolIcon { Symbol = _customPlayIcon };
                if (_audioPlayer?.PlaybackStopType == AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && LoopSound)
                    StartPlaying();
                else
                    StopProgressTimer();
            });
        }

        private void _headphonePlayer_PlaybackStopped() { }

        private void _audioPlayer_PlaybackResumed()
        {
            Dispatcher.Invoke(() =>
            {
                _playbackState = PlaybackState.Playing;
                PlayButton.Icon = new SymbolIcon { Symbol = SymbolRegular.Stop24 };
                _progressTimer.Start();
            });
        }

        private void _audioPlayer_PlaybackPaused()
        {
            Dispatcher.Invoke(() =>
            {
                _playbackState = PlaybackState.Paused;
                PlayButton.Icon = new SymbolIcon { Symbol = _customPlayIcon };
                _progressTimer.Stop();
            });
        }

        // ── Serialization ─────────────────────────────────────────────────────

        public JsonObject Serialize()
        {
            var jObj = new JsonObject();
            jObj.Add("LoopSound", LoopSound);
            jObj.Add("PlayThroughHeadphones", PlayThroughHeadphones);
            jObj.Add("soundFile", soundFile);
            jObj.Add("Title", Title);
            jObj.Add("CustomPlayIcon", _customPlayIcon.ToString());
            jObj.Add("ButtonVolume", _buttonVolume);
            jObj.Add("ButtonColor", _buttonColor);
            jObj.Add("FadeInSeconds", _fadeInSeconds);
            jObj.Add("FadeOutSeconds", _fadeOutSeconds);
            jObj.Add("FadeEnabled", _fadeEnabled);
            jObj.Add("AutoStopSeconds", _autoStopSeconds);
            if (_hotkeyId >= 0)
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
            {
                LoopSound = v.GetValue<bool>();
                LoopButton.Background = LoopSound ? Brushes.Blue : _unselectedBrush;
                LoopButton.MouseOverBackground = LoopSound ? Brushes.DarkBlue : _unselectedBrushHover;
                FadeButton.IsEnabled = !LoopSound;
            }
            if (jObj.TryGetPropertyValue("PlayThroughHeadphones", out v) && v != null)
            {
                PlayThroughHeadphones = v.GetValue<bool>();
                HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
                HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;
            }
            if (jObj.TryGetPropertyValue("soundFile", out v) && v != null)
                soundFile = v.GetValue<string>();
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
            {
                _buttonColor = v.GetValue<string>();
                if (!string.IsNullOrEmpty(_buttonColor))
                    RootBorder.Background = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_buttonColor));
            }
            if (jObj.TryGetPropertyValue("FadeInSeconds", out v) && v != null)
                _fadeInSeconds = v.GetValue<double>();
            if (jObj.TryGetPropertyValue("FadeOutSeconds", out v) && v != null)
                _fadeOutSeconds = v.GetValue<double>();
            if (jObj.TryGetPropertyValue("FadeEnabled", out v) && v != null)
            {
                _fadeEnabled = v.GetValue<bool>();
                FadeButton.Background = _fadeEnabled ? Brushes.Blue : _unselectedBrush;
                FadeButton.MouseOverBackground = _fadeEnabled ? Brushes.DarkBlue : _unselectedBrushHover;
            }
            if (jObj.TryGetPropertyValue("AutoStopSeconds", out v) && v != null)
                _autoStopSeconds = v.GetValue<double>();

            // Re-register hotkey if present
            if (jObj.TryGetPropertyValue("HotkeyModifiers", out v) && v != null)
                _hotkeyModifiers = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyVirtualKey", out v) && v != null)
                _hotkeyVirtualKey = v.GetValue<uint>();
            if (jObj.TryGetPropertyValue("HotkeyDisplay", out v) && v != null)
                _hotkeyDisplay = v.GetValue<string>();

            if (_hotkeyVirtualKey > 0 && MainWindow.Instance?.HotkeyManager != null)
            {
                _hotkeyId = MainWindow.Instance.HotkeyManager.Register(
                    _hotkeyModifiers, _hotkeyVirtualKey,
                    () => Dispatcher.Invoke(StartPlaying));
                UpdateHotkeyBadge();
            }
        }

        private void title_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (e.Source is TextBox textBox)
                Title = textBox.Text;
        }

        // ── Inner dialog windows ──────────────────────────────────────────────

        private sealed class HotkeyInputWindow : System.Windows.Window
        {
            private readonly System.Windows.Controls.TextBlock _label;

            public uint CapturedModifiers { get; private set; }
            public uint CapturedKey { get; private set; }
            public bool WasAssigned { get; private set; }
            public string HotkeyText { get; private set; } = string.Empty;

            public HotkeyInputWindow()
            {
                Title = "Assign Hotkey";
                Width = 300;
                Height = 110;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var panel = new System.Windows.Controls.StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };
                _label = new System.Windows.Controls.TextBlock
                {
                    Text = "Press a key combination…",
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var hint = new System.Windows.Controls.TextBlock
                {
                    Text = "(Esc to cancel)",
                    FontSize = 10,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Opacity = 0.6
                };
                panel.Children.Add(_label);
                panel.Children.Add(hint);
                Content = panel;

                KeyDown += OnKeyDown;
            }

            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                var key = e.Key == Key.System ? e.SystemKey : e.Key;

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
                WasAssigned = true;

                DialogResult = true;
                Close();
                e.Handled = true;
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

        private sealed class FadeSettingsWindow : System.Windows.Window
        {
            private readonly System.Windows.Controls.Slider _inSlider;
            private readonly System.Windows.Controls.Slider _outSlider;
            private readonly System.Windows.Controls.TextBlock _inLabel;
            private readonly System.Windows.Controls.TextBlock _outLabel;

            public double FadeIn => _inSlider.Value;
            public double FadeOut => _outSlider.Value;

            public FadeSettingsWindow(double fadeIn, double fadeOut)
            {
                Title = "Fade In / Out";
                Width = 300;
                Height = 200;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };

                _inLabel = new System.Windows.Controls.TextBlock { Text = $"Fade In: {fadeIn:F1}s" };
                _inSlider = new System.Windows.Controls.Slider
                {
                    Minimum = 0, Maximum = 10, Value = fadeIn,
                    TickFrequency = 0.5, IsSnapToTickEnabled = true
                };
                _inSlider.ValueChanged += (s, e) => _inLabel.Text = $"Fade In: {e.NewValue:F1}s";

                _outLabel = new System.Windows.Controls.TextBlock { Text = $"Fade Out: {fadeOut:F1}s", Margin = new Thickness(0, 8, 0, 0) };
                _outSlider = new System.Windows.Controls.Slider
                {
                    Minimum = 0, Maximum = 10, Value = fadeOut,
                    TickFrequency = 0.5, IsSnapToTickEnabled = true
                };
                _outSlider.ValueChanged += (s, e) => _outLabel.Text = $"Fade Out: {e.NewValue:F1}s";

                var ok = new System.Windows.Controls.Button
                {
                    Content = "OK", Width = 70,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                ok.Click += (s, e) => { DialogResult = true; Close(); };

                panel.Children.Add(_inLabel);
                panel.Children.Add(_inSlider);
                panel.Children.Add(_outLabel);
                panel.Children.Add(_outSlider);
                panel.Children.Add(ok);
                Content = panel;
            }
        }

        private sealed class AutoStopWindow : System.Windows.Window
        {
            private readonly System.Windows.Controls.Slider _slider;
            private readonly System.Windows.Controls.TextBlock _label;

            public double AutoStopSeconds => _slider.Value;

            public AutoStopWindow(double current)
            {
                Title = "Auto-Stop Timer";
                Width = 280;
                Height = 150;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(12) };

                _label = new System.Windows.Controls.TextBlock
                {
                    Text = current == 0 ? "Auto-stop: Disabled" : $"Auto-stop: {current:F0}s"
                };
                _slider = new System.Windows.Controls.Slider
                {
                    Minimum = 0, Maximum = 300, Value = current,
                    TickFrequency = 5
                };
                _slider.ValueChanged += (s, e) =>
                    _label.Text = e.NewValue == 0 ? "Auto-stop: Disabled" : $"Auto-stop: {e.NewValue:F0}s";

                var ok = new System.Windows.Controls.Button
                {
                    Content = "OK", Width = 70,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                ok.Click += (s, e) => { DialogResult = true; Close(); };

                panel.Children.Add(_label);
                panel.Children.Add(_slider);
                panel.Children.Add(ok);
                Content = panel;
            }
        }
    }
}
