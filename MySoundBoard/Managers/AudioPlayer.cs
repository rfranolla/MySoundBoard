using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace MySoundBoard.Managers
{
    public class AudioPlayer
    {
        public enum PlaybackStopTypes
        {
            PlaybackStoppedByUser, PlaybackStoppedReachingEndOfFile
        }

        public PlaybackStopTypes PlaybackStopType { get; set; }

        private WaveStream? _reader;
        private VolumeSampleProvider? _volumeProvider;
        private FadeInOutSampleProvider? _fadeProvider;
        private DirectSoundOut? _output;
        private readonly DirectSoundDeviceInfo _deviceInfo;
        private readonly string _filepath;
        private float _volume = 1.0f;

        public event Action? PlaybackResumed;
        public event Action? PlaybackStopped;
        public event Action? PlaybackPaused;

        public float Volume
        {
            get => _volume;
            set => _volume = value;
        }

        public AudioPlayer(string filepath, float volume, DirectSoundDeviceInfo deviceInfo)
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;
            _filepath = filepath;
            _volume = volume;
            _deviceInfo = deviceInfo;
            Initialize();
        }

        // Resolved once at startup; null if NAudio.Vorbis is not installed.
        private static readonly Type? _vorbisType =
            Type.GetType("NAudio.Vorbis.VorbisWaveReader, NAudio.Vorbis");

        private static WaveStream CreateReader(string filepath)
        {
            if (Path.GetExtension(filepath).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                && _vorbisType != null)
                return (WaveStream)Activator.CreateInstance(_vorbisType, filepath)!;
            return new AudioFileReader(filepath);
        }

        private void Initialize()
        {
            _reader?.Dispose();
            _reader = CreateReader(_filepath);

            ISampleProvider source = _reader.ToSampleProvider();
            _volumeProvider = new VolumeSampleProvider(source) { Volume = _volume };
            _fadeProvider = new FadeInOutSampleProvider(_volumeProvider, initiallySilent: false);

            _output?.Dispose();
            _output = new DirectSoundOut(_deviceInfo.Guid, 200);
            _output.PlaybackStopped += Output_PlaybackStopped;
            _output.Init(new SampleToWaveProvider(_fadeProvider));
        }

        public void BeginFadeIn(double durationMs)
            => _fadeProvider?.BeginFadeIn(durationMs);

        public void BeginFadeOut(double durationMs)
            => _fadeProvider?.BeginFadeOut(durationMs);

        public void Play(PlaybackState playbackState, double currentVolumeLevel)
        {
            if (playbackState == PlaybackState.Stopped || playbackState == PlaybackState.Paused)
                _output?.Play();

            if (_volumeProvider != null)
                _volumeProvider.Volume = (float)currentVolumeLevel;

            PlaybackResumed?.Invoke();
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Dispose();
            PlaybackStopped?.Invoke();
        }

        public void Stop() => _output?.Stop();

        public void Pause()
        {
            if (_output == null) return;
            _output.Pause();
            PlaybackPaused?.Invoke();
        }

        public void TogglePlayPause(double currentVolumeLevel)
        {
            if (_output != null)
            {
                if (_output.PlaybackState == PlaybackState.Playing)
                    Pause();
                else
                    Play(_output.PlaybackState, currentVolumeLevel);
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
                    _output.Stop();
                _output.Dispose();
                _output = null;
            }
            _reader?.Dispose();
            _reader = null;
        }

        public double GetLenghtInSeconds() => _reader?.TotalTime.TotalSeconds ?? 0;

        public double GetPositionInSeconds() => _reader?.CurrentTime.TotalSeconds ?? 0;

        public float GetVolume() => _volumeProvider?.Volume ?? 1f;

        public void SetPosition(double value)
        {
            if (_reader != null)
                _reader.CurrentTime = TimeSpan.FromSeconds(value);
        }

        public void SetVolume(float value)
        {
            _volume = value;
            if (_volumeProvider != null)
                _volumeProvider.Volume = value;
        }
    }
}
