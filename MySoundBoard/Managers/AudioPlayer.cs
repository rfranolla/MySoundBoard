using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace MySoundBoard.Managers
{
    /// <summary>Rewinds the underlying stream when it runs dry, keeping the output buffer full.</summary>
    internal sealed class LoopingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly WaveStream _stream;
        private readonly Func<bool> _shouldLoop;

        public LoopingSampleProvider(ISampleProvider source, WaveStream stream, Func<bool> shouldLoop)
        {
            _source = source;
            _stream = stream;
            _shouldLoop = shouldLoop;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _source.Read(buffer, offset + total, count - total);
                if (read == 0)
                {
                    if (!_shouldLoop() || _stream.Length == 0) break;
                    _stream.Position = 0;
                    // An empty read right after a rewind means there's nothing to loop.
                    read = _source.Read(buffer, offset + total, count - total);
                    if (read == 0) break;
                }
                total += read;
            }
            return total;
        }
    }

    public class AudioPlayer
    {
        public enum PlaybackStopTypes
        {
            PlaybackStoppedByUser, PlaybackStoppedReachingEndOfFile
        }

        /// <summary>Why playback last ended; a user stop or fade-out must not trigger a loop restart.</summary>
        public PlaybackStopTypes PlaybackStopType { get; private set; } = PlaybackStopTypes.PlaybackStoppedReachingEndOfFile;

        private WaveStream? _reader;
        private VolumeSampleProvider? _volumeProvider;
        private FadeInOutSampleProvider? _fadeProvider;
        private DirectSoundOut? _output;
        private readonly DirectSoundDeviceInfo _deviceInfo;
        private readonly string _filepath;
        private float _volume = 1.0f;

        public event Action? PlaybackStopped;

        public float Volume
        {
            get => _volume;
            set => _volume = value;
        }

        /// <summary>When true, the source rewinds at end of file so looping is gapless.</summary>
        public bool Loop { get; set; }

        public AudioPlayer(string filepath, float volume, DirectSoundDeviceInfo deviceInfo, bool loop = false)
        {
            Loop = loop;
            _filepath = filepath;
            _volume = volume;
            _deviceInfo = deviceInfo;
            Initialize();
        }

        private static WaveStream CreateReader(string filepath)
        {
            if (Path.GetExtension(filepath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                return new NAudio.Vorbis.VorbisWaveReader(filepath);
            return new AudioFileReader(filepath);
        }

        private void Initialize()
        {
            _reader?.Dispose();
            _reader = CreateReader(_filepath);

            ISampleProvider source = new LoopingSampleProvider(_reader.ToSampleProvider(), _reader, () => Loop);
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
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedByUser;
            _fadeProvider?.BeginFadeOut(durationMs);
        }

        /// <summary>Starts playback from the beginning. A player is single-use: once stopped it disposes itself.</summary>
        public void Play()
        {
            if (_output?.PlaybackState == PlaybackState.Stopped)
                _output.Play();
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Dispose();
            PlaybackStopped?.Invoke();
        }

        public void Stop()
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedByUser;
            _output?.Stop();
        }

        public void Dispose()
        {
            // Detach first so Stop() cannot re-enter Dispose or notify an owner that is tearing us down.
            var output = _output;
            _output = null;
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                if (output.PlaybackState != PlaybackState.Stopped)
                    output.Stop();
                output.Dispose();
            }
            _reader?.Dispose();
            _reader = null;
        }

        public double GetLengthInSeconds() => _reader?.TotalTime.TotalSeconds ?? 0;

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
