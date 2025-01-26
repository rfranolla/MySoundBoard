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
        private bool PlayThroughHeadphones = true;
        private bool IsPlaying = false;

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

            IsPlaying = !IsPlaying;
        }

        private void StartPlaying()
        {
            if (string.IsNullOrEmpty(soundFile))
            {
                return;
            }

            if (_playbackState == PlaybackState.Stopped)
            {
                _audioPlayer = new AudioPlayer(soundFile, MainWindow.Instance.Volume/100, MainWindow.GetSelectedOutputDevice());
                _audioPlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                CurrentTrackLenght = _audioPlayer.GetLenghtInSeconds();
                if (PlayThroughHeadphones)
                {
                    _headphonePlayer = new AudioPlayer(soundFile, MainWindow.Instance.Volume/100, MainWindow.GetSelectedHeadphoneDevice());
                    _headphonePlayer.PlaybackStopType = AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
                    _headphonePlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                    _headphonePlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                    _headphonePlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                }

                _audioPlayer.TogglePlayPause(MainWindow.Instance.Volume/100);
                if (PlayThroughHeadphones)
                    _headphonePlayer.TogglePlayPause(MainWindow.Instance.Volume/100);

                PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Pause48 };
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

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("EditButton Click");

            // Open dialog to pick sound
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "sound files (*.mp3)|*.mp3|(*.wav)|*.wav|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                DialogResult result = openFileDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
                {
                    // Set this title to sound file name
                    title.Text = openFileDialog.SafeFileName;
                    Title = title.Text;

                    soundFile = openFileDialog.FileName;

                    Debug.WriteLine(soundFile);
                }
            }
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("LoopButton Click");
            LoopSound = !LoopSound;
            LoopButton.Background = LoopSound ? Brushes.Blue : _unselectedBrush;
            LoopButton.MouseOverBackground = LoopSound ? Brushes.DarkBlue : _unselectedBrushHover;
            Debug.WriteLine("Sound will loop: "+ LoopSound);
            // Visual cue to show loop sound
        }

        private void HeadphoneButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("HeadphoneButton Click");
            PlayThroughHeadphones = !PlayThroughHeadphones;
            HeadPhoneButton.Background = PlayThroughHeadphones ? Brushes.Blue : _unselectedBrush;
            HeadPhoneButton.MouseOverBackground = PlayThroughHeadphones ? Brushes.DarkBlue : _unselectedBrushHover;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("DeleteButton Click");
            MainWindow.RemoveButton(this);
            if (_audioPlayer != null)
                _audioPlayer.Dispose();
            if(_headphonePlayer != null)
                _headphonePlayer.Dispose();
        }

        #region Sound Events

        private void _audioPlayer_PlaybackStopped()
        {
            _playbackState = PlaybackState.Stopped;
            PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Play48 };
            if (_audioPlayer.PlaybackStopType == AudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && LoopSound)
            {
                StartPlaying();
            }
        }

        private void _audioPlayer_PlaybackResumed()
        {
            _playbackState = PlaybackState.Playing;
            PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Pause48 };
        }

        private void _audioPlayer_PlaybackPaused()
        {
            _playbackState = PlaybackState.Paused;
            PlayButton.Icon = new SymbolIcon() { Symbol = SymbolRegular.Play48 };
        }

        #endregion

        public JsonObject Serialize()
        {
            var jObj = new JsonObject();

            jObj.Add("LoopSound", LoopSound);
            jObj.Add("PlaybackState", _playbackState.ToString());
            jObj.Add("PlayThroughHeadphones", PlayThroughHeadphones);
            jObj.Add("soundFile", soundFile);
            jObj.Add("Title", Title);

            return jObj;
        }

        private void Deserialized(JsonObject jObj)
        {
            JsonNode? nodeValue;
            if (jObj.TryGetPropertyValue("LoopSound", out nodeValue))
            {
                if (nodeValue != null)
                {
                    LoopSound = nodeValue.GetValue<bool>();
                }
            }

            if (jObj.TryGetPropertyValue("PlaybackState", out nodeValue))
            {
                if (nodeValue != null)
                {
                    Enum.TryParse(nodeValue.GetValue<string>(), out _playbackState);
                }
            }

            if (jObj.TryGetPropertyValue("PlayThroughHeadphones", out nodeValue))
            {
                if (nodeValue != null)
                {
                    PlayThroughHeadphones = nodeValue.GetValue<bool>();
                }
            }

            if (jObj.TryGetPropertyValue("soundFile", out nodeValue))
            {
                if (nodeValue != null)
                {
                    soundFile = nodeValue.GetValue<string>();
                }
            }

            if (jObj.TryGetPropertyValue("Title", out nodeValue))
            {
                if (nodeValue != null)
                {
                    Title = nodeValue.GetValue<string>();
                    this.title.Text = Title;
                }
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
