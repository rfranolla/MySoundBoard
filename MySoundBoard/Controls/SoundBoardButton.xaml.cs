using MySoundBoard.Managers;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace MySoundBoard.Controls
{
    /// <summary>
    /// Interaction logic for SoundBoardButton.xaml
    /// </summary>
    public partial class SoundBoardButton : UserControl
    {
        #region Properties

        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState _playbackState;

        private bool LoopSound = false;
        private bool PlayThroughHeadphones = false;

        private string soundFile = string.Empty;

        private AudioPlayer _audioPlayer;
        private AudioPlayer _headphonePlayer;

        private Brush _unselectedBrush;
        private Brush _unselectedBrushHover;

        public string Title { get; set; } = string.Empty;

        public double CurrentTrackLenght
        {
            get; set;
        }

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

            MainWindow.Instance.ThemeChanged += ThemeChanged_Event;
        }

        private void ThemeChanged_Event(object? sender, RoutedEventArgs e)
        {
            var tmp = new Button();
            _unselectedBrush = tmp.Background;
            _unselectedBrushHover = tmp.MouseOverBorderBrush;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            StartPlaying();
        }

        private void StartPlaying()
        {
            if (string.IsNullOrEmpty(soundFile))
            {
                return;
            }

            if (_playbackState == PlaybackState.Paused)
            {
                _audioPlayer?.TogglePlayPause(MainWindow.Instance.Volume / 100);
                _headphonePlayer?.TogglePlayPause(MainWindow.Instance.Volume / 100);
                return;
            }

            if (_playbackState == PlaybackState.Stopped)
            {
                try
                {
                    var outputDevice = MainWindow.GetSelectedOutputDevice();
                    var headphoneDevice = MainWindow.GetSelectedHeadphoneDevice();
                    bool useDualOutput = PlayThroughHeadphones
                        && headphoneDevice != null
                        && headphoneDevice.Guid != outputDevice?.Guid;

                    _audioPlayer = new AudioPlayer(soundFile, MainWindow.Instance.Volume / 100, outputDevice);
                    _audioPlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                    _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                    _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                    _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                    CurrentTrackLenght = _audioPlayer.GetLenghtInSeconds();

                    if (useDualOutput)
                    {
                        _headphonePlayer = new AudioPlayer(soundFile, MainWindow.Instance.Volume / 100, headphoneDevice);
                        _headphonePlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                        _headphonePlayer.PlaybackStopped += _headphonePlayer_PlaybackStopped;
                    }

                    _audioPlayer.TogglePlayPause(MainWindow.Instance.Volume / 100);
                    if (useDualOutput)
                        _headphonePlayer.TogglePlayPause(MainWindow.Instance.Volume / 100);

                    PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Pause48 };
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
        }

        public void UpdateVolume(float volume)
        {
            _audioPlayer?.SetVolume(volume);
            _headphonePlayer?.SetVolume(volume);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("EditButton Click");

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                openFileDialog.Filter = "sound files (*.mp3)|*.mp3|(*.wav)|*.wav|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                DialogResult result = openFileDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
                {
                    title.Text = openFileDialog.SafeFileName;
                    Title = title.Text;
                    soundFile = openFileDialog.FileName;
                }
            }
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            LoopSound = !LoopSound;
            LoopButton.Background = LoopSound ? Brushes.Blue : _unselectedBrush;
            LoopButton.MouseOverBackground = LoopSound ? Brushes.DarkBlue : _unselectedBrushHover;
        }

        private void HeadphoneButton_Click(object sender, RoutedEventArgs e)
        {
            PlayThroughHeadphones = !PlayThroughHeadphones;
            HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
            HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.RemoveButton(this);
            _audioPlayer?.Dispose();
            _headphonePlayer?.Dispose();
        }

        #region Sound Events

        private void _audioPlayer_PlaybackStopped()
        {
            Dispatcher.Invoke(() =>
            {
                _playbackState = PlaybackState.Stopped;
                PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Play48 };
                if (_audioPlayer.PlaybackStopType == AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && LoopSound)
                {
                    StartPlaying();
                }
            });
        }

        private void _headphonePlayer_PlaybackStopped()
        {
            // Headphone player cleanup only — loop logic is driven by _audioPlayer
        }

        private void _audioPlayer_PlaybackResumed()
        {
            Dispatcher.Invoke(() =>
            {
                _playbackState = PlaybackState.Playing;
                PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Pause48 };
            });
        }

        private void _audioPlayer_PlaybackPaused()
        {
            Dispatcher.Invoke(() =>
            {
                _playbackState = PlaybackState.Paused;
                PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Play48 };
            });
        }

        #endregion

        public JsonObject Serialize()
        {
            var jObj = new JsonObject();

            jObj.Add("LoopSound", LoopSound);
            jObj.Add("PlayThroughHeadphones", PlayThroughHeadphones);
            jObj.Add("soundFile", soundFile);
            jObj.Add("Title", Title);

            return jObj;
        }

        private void Deserialized(JsonObject jObj)
        {
            JsonNode? nodeValue;

            if (jObj.TryGetPropertyValue("LoopSound", out nodeValue) && nodeValue != null)
            {
                LoopSound = nodeValue.GetValue<bool>();
                LoopButton.Background = LoopSound ? Brushes.Blue : _unselectedBrush;
                LoopButton.MouseOverBackground = LoopSound ? Brushes.DarkBlue : _unselectedBrushHover;
            }

            if (jObj.TryGetPropertyValue("PlayThroughHeadphones", out nodeValue) && nodeValue != null)
            {
                PlayThroughHeadphones = nodeValue.GetValue<bool>();
                HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
                HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;
            }

            if (jObj.TryGetPropertyValue("soundFile", out nodeValue) && nodeValue != null)
            {
                soundFile = nodeValue.GetValue<string>();
            }

            if (jObj.TryGetPropertyValue("Title", out nodeValue) && nodeValue != null)
            {
                Title = nodeValue.GetValue<string>();
                this.title.Text = Title;
            }
        }

        private void title_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (e.Source is TextBox textBox)
            {
                Title = textBox.Text;
            }
        }
    }
}
