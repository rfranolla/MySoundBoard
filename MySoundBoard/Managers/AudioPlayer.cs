using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace MySoundBoard.Managers
{
    /// <summary>
    /// Plays the region [start, end) of the underlying stream, rewinding to start when it runs dry
    /// (if looping) so the output buffer stays full and loops are gapless.
    /// </summary>
    internal sealed class LoopingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly WaveStream _stream;
        private readonly Func<bool> _shouldLoop;
        private readonly long _start;
        private readonly long _end;

        /// <param name="startPosition">Region start in stream bytes.</param>
        /// <param name="endPosition">Region end in stream bytes; 0 means the end of the stream.</param>
        public LoopingSampleProvider(ISampleProvider source, WaveStream stream, Func<bool> shouldLoop,
            long startPosition = 0, long endPosition = 0)
        {
            _source = source;
            _stream = stream;
            _shouldLoop = shouldLoop;
            _start = startPosition;
            _end = endPosition;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        private long End => _end > 0 ? Math.Min(_end, _stream.Length) : _stream.Length;

        public int Read(float[] buffer, int offset, int count)
        {
            int total = 0;
            bool justRewound = false;
            while (total < count)
            {
                int read = ReadUntilEnd(buffer, offset + total, count - total);
                if (read == 0)
                {
                    // An empty read right after a rewind means there's nothing to loop.
                    if (justRewound || !_shouldLoop() || End <= _start) break;
                    _stream.Position = _start;
                    justRewound = true;
                    continue;
                }
                justRewound = false;
                total += read;
            }
            return total;
        }

        private int ReadUntilEnd(float[] buffer, int offset, int count)
        {
            long remainingFrames = (End - _stream.Position) / _stream.WaveFormat.BlockAlign;
            if (remainingFrames <= 0) return 0;
            long remainingSamples = remainingFrames * _source.WaveFormat.Channels;
            return _source.Read(buffer, offset, (int)Math.Min(count, remainingSamples));
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
        private readonly double _trimStartSeconds;
        private readonly double _trimEndSeconds;
        private float _volume = 1.0f;

        public event Action? PlaybackStopped;

        public float Volume
        {
            get => _volume;
            set => _volume = value;
        }

        /// <summary>When true, the source rewinds at the end of the region so looping is gapless.</summary>
        public bool Loop { get; set; }

        /// <param name="trimStartSeconds">Where playback (and each loop) starts.</param>
        /// <param name="trimEndSeconds">Where playback ends; 0 plays to the end of the file.</param>
        public AudioPlayer(string filepath, float volume, DirectSoundDeviceInfo deviceInfo, bool loop = false,
            double trimStartSeconds = 0, double trimEndSeconds = 0)
        {
            Loop = loop;
            _filepath = filepath;
            _volume = volume;
            _deviceInfo = deviceInfo;
            _trimStartSeconds = Math.Max(0, trimStartSeconds);
            _trimEndSeconds = Math.Max(0, trimEndSeconds);
            Initialize();
        }

        private static WaveStream CreateReader(string filepath)
        {
            if (Path.GetExtension(filepath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
                return new NAudio.Vorbis.VorbisWaveReader(filepath);
            return new AudioFileReader(filepath);
        }

        /// <summary>Length of an audio file in seconds, without creating an output device.</summary>
        public static double GetFileDurationSeconds(string filepath)
        {
            using var reader = CreateReader(filepath);
            return reader.TotalTime.TotalSeconds;
        }

        private static long ToStreamPosition(WaveStream stream, double seconds)
        {
            var format = stream.WaveFormat;
            long position = (long)(seconds * format.AverageBytesPerSecond);
            return Math.Clamp(position - position % format.BlockAlign, 0, stream.Length);
        }

        private void Initialize()
        {
            try
            {
                _reader = CreateReader(_filepath);

                long start = ToStreamPosition(_reader, _trimStartSeconds);
                long end = _trimEndSeconds > 0 ? ToStreamPosition(_reader, _trimEndSeconds) : 0;
                _reader.Position = start;

                ISampleProvider source = new LoopingSampleProvider(_reader.ToSampleProvider(), _reader, () => Loop, start, end);
                _volumeProvider = new VolumeSampleProvider(source) { Volume = _volume };
                _fadeProvider = new FadeInOutSampleProvider(_volumeProvider, initiallySilent: false);

                _output = new DirectSoundOut(_deviceInfo.Guid, 200);
                _output.PlaybackStopped += Output_PlaybackStopped;
                _output.Init(new SampleToWaveProvider(_fadeProvider));
            }
            catch
            {
                // The caller never gets an instance to dispose, so release the file and device here.
                Dispose();
                throw;
            }
        }

        public void BeginFadeIn(double durationMs)
            => _fadeProvider?.BeginFadeIn(durationMs);

        public void BeginFadeOut(double durationMs)
        {
            PlaybackStopType = PlaybackStopTypes.PlaybackStoppedByUser;
            _fadeProvider?.BeginFadeOut(durationMs);
        }

        /// <summary>Starts playback from the trim start. A player is single-use: once stopped it disposes itself.</summary>
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

        /// <summary>Length of the whole file.</summary>
        public double GetLengthInSeconds() => _reader?.TotalTime.TotalSeconds ?? 0;

        /// <summary>Position within the whole file.</summary>
        public double GetPositionInSeconds() => _reader?.CurrentTime.TotalSeconds ?? 0;

        /// <summary>Length of the part that plays, after trimming.</summary>
        public double GetRegionLengthInSeconds()
        {
            double total = GetLengthInSeconds();
            double end = _trimEndSeconds > 0 ? Math.Min(_trimEndSeconds, total) : total;
            return Math.Max(0, end - Math.Min(_trimStartSeconds, total));
        }

        /// <summary>Position measured from the trim start.</summary>
        public double GetRegionPositionInSeconds() => Math.Max(0, GetPositionInSeconds() - _trimStartSeconds);

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
