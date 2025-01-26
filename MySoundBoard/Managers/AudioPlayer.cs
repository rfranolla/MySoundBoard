using NAudio.Wave;

namespace MySoundBoard.Managers
{
    public class AudioPlayer
    {
        public enum PlaybackStopTypes
        {
            PlaybackStoppedByUser, PlaybackStoppedReachingEndOfFile
        }

        public PlaybackStopTypes PlaybackStopType { get; set; }

        private AudioFileReader _audioFileReader;

        private DirectSoundOut _output;

        private DirectSoundDeviceInfo _deviceInfo;

        private string _filepath;
        private float _volume = 1.0f;

        public event Action PlaybackResumed;
        public event Action PlaybackStopped;
        public event Action PlaybackPaused;

        public string FilePath
        {
            get => _filepath;
            set
            {
                _filepath = value;
                Initialize();
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                Initialize();
            }
        }


        public AudioPlayer(string filepath, float volume, DirectSoundDeviceInfo deviceInfo)
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
            _filepath = filepath;
            _volume = volume;
            _deviceInfo = deviceInfo;
            Initialize();
        }

        public AudioPlayer()
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
        }

        private void Initialize()
        {
            if (_audioFileReader != null) 
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
            _audioFileReader = new AudioFileReader(_filepath) { Volume = _volume };

            if (_output != null)
            {
                _output.Dispose();
                _output = null;
            }
            _output = new DirectSoundOut(_deviceInfo.Guid, 200);
            _output.PlaybackStopped += _output_PlaybackStopped;

            var wc = new WaveChannel32(_audioFileReader);
            wc.PadWithZeroes = false;

            _output.Init(wc);
        }

        public void Play(PlaybackState playbackState, double currentVolumeLevel)
        {
            if (playbackState == PlaybackState.Stopped || playbackState == PlaybackState.Paused)
            {
                _output.Play();
            }

            _audioFileReader.Volume = (float)currentVolumeLevel;

            if (PlaybackResumed != null)
            {
                PlaybackResumed();
            }
        }

        private void _output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispose();
            if (PlaybackStopped != null)
            {
                PlaybackStopped();
            }
        }

        public void Stop()
        {
            if (_output != null)
            {
                _output.Stop();
            }
        }

        public void Pause()
        {
            if (_output != null)
            {
                _output.Pause();

                if (PlaybackPaused != null)
                {
                    PlaybackPaused();
                }
            }
        }

        public void TogglePlayPause(double currentVolumeLevel)
        {
            if (_output != null)
            {
                if (_output.PlaybackState == PlaybackState.Playing)
                {
                    Pause();
                }
                else
                {
                    Play(_output.PlaybackState, currentVolumeLevel);
                }
            }
            else
            {
                Play(PlaybackState.Stopped, currentVolumeLevel);
            }
        }

        public void Dispose()
        {
            if (_output != null)
            {
                if (_output.PlaybackState == PlaybackState.Playing)
                {
                    _output.Stop();
                }
                _output.Dispose();
                _output = null;
            }
            if (_audioFileReader != null)
            {
                _audioFileReader.Dispose();
                _audioFileReader = null;
            }
        }

        public double GetLenghtInSeconds()
        {
            if (_audioFileReader != null)
            {
                return _audioFileReader.TotalTime.TotalSeconds;
            }
            else
            {
                return 0;
            }
        }

        public double GetPositionInSeconds()
        {
            return _audioFileReader != null ? _audioFileReader.CurrentTime.TotalSeconds : 0;
        }

        public float GetVolume()
        {
            if (_audioFileReader != null)
            {
                return _audioFileReader.Volume;
            }
            return 1;
        }

        public void SetPosition(double value)
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.FromSeconds(value);
            }
        }

        public void SetVolume(float value)
        {
            if (_output != null)
            {
                _audioFileReader.Volume = value;
            }
        }
    }
}
