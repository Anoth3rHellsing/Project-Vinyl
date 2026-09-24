using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using ProjectVinyl.Models;
using SoundTouch;

namespace ProjectVinyl.Services;

/// <summary>
/// Rewritten audio playback service using WasapiOut (event-driven, shared mode)
/// + SoundTouch Tempo (time-stretch without pitch shift) + DSP chain.
///
/// Fixes:
/// - Speed bug: Uses Tempo instead of Rate. Tempo=1.0 is true passthrough.
/// - Distortion: Proper buffer management with 50ms source reads.
/// - 2-second playback: Explicit sample rate matching via WdlResamplingSampleProvider.
/// - Volume: Software-only via VolumeSampleProvider, never touches device volume.
/// </summary>
public class AudioPlayerService : IDisposable
{
    private AudioFileReader? _reader;
    private IWavePlayer? _output;
    private CancellationTokenSource? _positionCts;
    private readonly object _lock = new();
    private bool _disposed;
    private float _playbackRate = 1.0f;
    private float _volume = 0.5f;

    // DSP chain components (always in chain, toggled via parameters)
    private TimeStretchProvider? _speedProvider;
    private BassBoostEffect? _bassBoost;
    private ReverbEffect? _reverb;
    private VolumeSampleProvider? _volumeProvider;
    private TeeSampleProvider? _teeProvider;

    // DSP state fields
    private float _reverbMix = 0f;
    private float _reverbMixTarget = 0.3f;
    private float _bassBoostGain = 0f;
    private float _bassBoostGainTarget = 6f;

    public bool IsPlaying { get; private set; }
    public TimeSpan CurrentPosition { get; private set; }
    public TimeSpan TotalDuration { get; private set; }

    public double Volume
    {
        get => _volume;
        set
        {
            var clamped = (float)Math.Clamp(value, 0.0, 1.0);
            _volume = clamped;
            lock (_lock)
            {
                if (_volumeProvider != null)
                    _volumeProvider.Volume = _volume;
            }
        }
    }

    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            var clamped = (float)Math.Clamp(value, 0.5, 2.0);
            _playbackRate = clamped;
            lock (_lock)
            {
                if (_speedProvider != null)
                    _speedProvider.Tempo = _playbackRate;
            }
        }
    }

    // DSP: Reverb
    public bool IsReverbEnabled
    {
        get => _reverbMix > 0.001f;
        set
        {
            lock (_lock)
            {
                _reverbMix = value ? _reverbMixTarget : 0f;
                if (_reverb != null)
                    _reverb.WetDryMix = _reverbMix;
            }
        }
    }

    public double ReverbMix
    {
        get => _reverbMixTarget;
        set
        {
            _reverbMixTarget = (float)Math.Clamp(value, 0.0, 1.0);
            lock (_lock)
            {
                if (_reverb != null && _reverbMix > 0.001f)
                    _reverb.WetDryMix = _reverbMixTarget;
            }
        }
    }

    // DSP: Bass Boost
    public bool IsBassBoostEnabled
    {
        get => _bassBoostGain > 0.01f;
        set
        {
            lock (_lock)
            {
                _bassBoostGain = value ? _bassBoostGainTarget : 0f;
                if (_bassBoost != null)
                    _bassBoost.GainDb = _bassBoostGain;
            }
        }
    }

    public double BassBoostGain
    {
        get => _bassBoostGainTarget;
        set
        {
            _bassBoostGainTarget = (float)Math.Clamp(value, 0.0, 12.0);
            lock (_lock)
            {
                if (_bassBoost != null && _bassBoostGain > 0.01f)
                    _bassBoost.GainDb = _bassBoostGainTarget;
            }
        }
    }

    public event Action? PositionChanged;
    public event Action? PlaybackStopped;
    public event Action<ISampleProvider?>? SampleProviderChanged;

    public void Play(Track track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.FilePath))
            return;

        lock (_lock)
        {
            StopInternal();

            try
            {
                _reader = new AudioFileReader(track.FilePath);
                TotalDuration = _reader.TotalTime;
                CurrentPosition = TimeSpan.Zero;

                AudioLogService.Write("PLAY_START", $"Track: {track.Title} | File: {track.FilePath}");
                AudioLogService.Write("PLAY_START", $"SourceFormat: SampleRate={_reader.WaveFormat.SampleRate} Channels={_reader.WaveFormat.Channels} BitsPerSample={_reader.WaveFormat.BitsPerSample} Duration={_reader.TotalTime}");
                AudioLogService.Write("PLAY_START", $"RequestedTempo: {_playbackRate:F3} | Volume: {_volume:F3}");
                AudioLogService.Write("PLAY_START", $"DSP: Reverb={IsReverbEnabled}(Mix={_reverbMix:F2}) BassBoost={IsBassBoostEnabled}(Gain={_bassBoostGain:F1}dB Cutoff={100}Hz)");

                // Initialize output device FIRST to know the target sample rate
                string outputType;
                int outputSampleRate;
                try
                {
                    var wasapi = new WasapiOut();
                    _output = wasapi;
                    outputSampleRate = wasapi.OutputWaveFormat.SampleRate;
                    outputType = $"WasapiOut(SampleRate={wasapi.OutputWaveFormat.SampleRate} Channels={wasapi.OutputWaveFormat.Channels} BitsPerSample={wasapi.OutputWaveFormat.BitsPerSample})";
                }
                catch (Exception ex)
                {
                    AudioLogService.Write("OUTPUT_FALLBACK", $"WasapiOut init failed: {ex.Message}. Falling back to WaveOut.");
                    var waveOut = new WaveOut();
                    _output = waveOut;
                    outputSampleRate = 44100;
                    outputType = "WaveOut(default)";
                }

                AudioLogService.Write("OUTPUT_INIT", $"Device: {outputType}");

                if (_reader.WaveFormat.SampleRate != outputSampleRate)
                {
                    AudioLogService.Write("RESAMPLE", $"Source={_reader.WaveFormat.SampleRate}Hz → Output={outputSampleRate}Hz — explicit WdlResamplingSampleProvider inserted");
                }

                // Build chain WITH explicit resampler targeting output sample rate
                var provider = BuildSampleProviderChain(_reader, outputSampleRate);

                // Now init the output with the fully-built chain
                _output.Init(provider);

                _output.PlaybackStopped += OnOutputPlaybackStopped;
                _output.Play();
                IsPlaying = true;

                AudioLogService.Write("PLAY_STARTED", $"Playback started successfully at tempo={_playbackRate:F3}");
                StartPositionTracking();
                SampleProviderChanged?.Invoke((ISampleProvider?)_teeProvider ?? _reader);
            }
            catch
            {
                StopInternal();
                throw;
            }
        }
    }

    public void ChangePlaybackRate(float newRate)
    {
        PlaybackRate = newRate;
    }

    private ISampleProvider BuildSampleProviderChain(AudioFileReader reader, int outputSampleRate)
    {
        ISampleProvider current = reader;

        // 0. Explicit resampling to match output device sample rate
        if (reader.WaveFormat.SampleRate != outputSampleRate)
        {
            AudioLogService.Write("RESAMPLE_CHAIN", $"Inserting WdlResamplingSampleProvider: {reader.WaveFormat.SampleRate}Hz → {outputSampleRate}Hz");
            current = new WdlResamplingSampleProvider(current, outputSampleRate);
        }

        // 1. Time-stretch (speed control without pitch change)
        _speedProvider?.Dispose();
        _speedProvider = new TimeStretchProvider(current, _playbackRate);
        current = _speedProvider;

        // 2. Bass Boost (always in chain, GainDb=0 when disabled)
        _bassBoost = new BassBoostEffect(current, _bassBoostGain);
        current = _bassBoost;

        // 3. Reverb (always in chain, WetDryMix=0 when disabled)
        _reverb = new ReverbEffect(current, 1.5f);
        _reverb.WetDryMix = _reverbMix;
        current = _reverb;

        // 4. Tee for visualizer (non-destructive tap)
        _teeProvider?.Dispose();
        _teeProvider = new TeeSampleProvider(current);
        current = _teeProvider;

        // 5. Software volume control
        _volumeProvider = new VolumeSampleProvider(current) { Volume = _volume };
        current = _volumeProvider;

        return current;
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_output != null && IsPlaying)
            {
                _output.Pause();
                IsPlaying = false;
                StopPositionTracking();
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_output != null && !IsPlaying && _reader != null)
            {
                _output.Play();
                IsPlaying = true;
                StartPositionTracking();
            }
        }
    }

    public void Stop()
    {
        lock (_lock) { StopInternal(); }
    }

    public void Seek(TimeSpan position)
    {
        lock (_lock)
        {
            if (_reader != null)
            {
                try
                {
                    _reader.CurrentTime = position;
                    CurrentPosition = position;
                    _speedProvider?.Reposition();
                }
                catch { }
            }
        }

        PositionChanged?.Invoke();
    }

    private void StartPositionTracking()
    {
        _positionCts?.Cancel();
        _positionCts = new CancellationTokenSource();
        var token = _positionCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, token);
                    lock (_lock)
                    {
                        if (_reader != null && IsPlaying)
                            CurrentPosition = _reader.CurrentTime;
                    }
                    PositionChanged?.Invoke();
                }
                catch (OperationCanceledException) { break; }
            }
        }, token);
    }

    private void StopPositionTracking()
    {
        _positionCts?.Cancel();
        _positionCts = null;
    }

    private void OnOutputPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            IsPlaying = false;
            StopPositionTracking();
        }
        PlaybackStopped?.Invoke();
    }

    private void StopInternal()
    {
        StopPositionTracking();

        if (_output != null)
        {
            _output.PlaybackStopped -= OnOutputPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _speedProvider?.Dispose();
        _speedProvider = null;
        _bassBoost = null;
        _reverb = null;
        _volumeProvider = null;
        _teeProvider = null;

        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }

        IsPlaying = false;
        CurrentPosition = TimeSpan.Zero;
        TotalDuration = TimeSpan.Zero;

        SampleProviderChanged?.Invoke(null);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            lock (_lock) { StopInternal(); }
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Time-stretch provider using SoundTouch Tempo (speed change WITHOUT pitch shift).
/// Unlike Rate (vinyl-style), Tempo preserves pitch at any speed.
/// At Tempo=1.0, processing is minimal near-passthrough.
/// </summary>
internal sealed class TimeStretchProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _sourceProvider;
    private readonly SoundTouchProcessor _soundTouch;
    private readonly float[] _sourceReadBuffer;
    private readonly int _channelCount;
    private float _tempo;
    private bool _repositionRequested;
    private bool _disposed;

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public float Tempo
    {
        get => _tempo;
        set
        {
            if (Math.Abs(_tempo - value) > 0.001f)
            {
                _tempo = Math.Clamp(value, 0.5f, 2.0f);
                _soundTouch.Tempo = _tempo;
            }
        }
    }

    public TimeStretchProvider(ISampleProvider sourceProvider, float tempo)
    {
        _sourceProvider = sourceProvider;
        _tempo = Math.Clamp(tempo, 0.5f, 2.0f);
        _channelCount = sourceProvider.WaveFormat.Channels;

        _soundTouch = new SoundTouchProcessor
        {
            SampleRate = sourceProvider.WaveFormat.SampleRate,
            Channels = _channelCount,
            Tempo = _tempo
        };

        // 200ms source buffer — SoundTouch needs multiple processing windows to prime cleanly
        int readDurationMs = 200;
        int sourceBufferSize = (sourceProvider.WaveFormat.SampleRate * _channelCount * readDurationMs) / 1000;
        _sourceReadBuffer = new float[sourceBufferSize];

        AudioLogService.Write("TIMESTRETCH_INIT", $"SourceSR={sourceProvider.WaveFormat.SampleRate} Ch={_channelCount} Tempo={_tempo:F3} ReadBufSamples={sourceBufferSize} ReadBufMs={readDurationMs}");
    }

    public void Reposition()
    {
        _repositionRequested = true;
    }

    public int Read(Span<float> buffer)
    {
        if (_disposed)
            return 0;

        if (_repositionRequested)
        {
            _soundTouch.Clear();
            _repositionRequested = false;
        }

        int samplesWritten = 0;
        bool reachedEndOfSource = false;
        int emptyReceiveCount = 0; // Guard against infinite loop when SoundTouch is priming

        while (samplesWritten < buffer.Length)
        {
            int desiredFrames = (buffer.Length - samplesWritten) / _channelCount;
            if (desiredFrames <= 0)
                break;

            // Try to receive output first
            var outputSlice = buffer.Slice(samplesWritten);
            var received = _soundTouch.ReceiveSamples(outputSlice, desiredFrames) * _channelCount;

            if (received > 0)
            {
                samplesWritten += received;
                emptyReceiveCount = 0;
                continue;
            }

            // No output available — feed more input or flush
            if (!reachedEndOfSource)
            {
                var readFromSource = _sourceProvider.Read(_sourceReadBuffer.AsSpan());
                if (readFromSource > 0)
                {
                    var inputSpan = _sourceReadBuffer.AsSpan(0, readFromSource);
                    _soundTouch.PutSamples(inputSpan, readFromSource / _channelCount);
                    emptyReceiveCount = 0;
                }
                else
                {
                    reachedEndOfSource = true;
                    _soundTouch.Flush();
                }
            }
            else
            {
                // End of source and no more output — done
                break;
            }

            // Safety: if SoundTouch keeps returning 0 after many feed attempts,
            // it may be in a bad state. Break to avoid infinite loop.
            emptyReceiveCount++;
            if (emptyReceiveCount > 200)
                break;
        }

        return samplesWritten;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _soundTouch.Clear();
        }
    }
}

/// <summary>
/// Non-destructive sample provider tap for visualizer.
/// Kept from original implementation — works correctly.
/// </summary>
internal sealed class TeeSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly float[] _ringBuffer;
    private int _writePos;
    private int _readPos;
    private int _available;
    private readonly object _lock = new();

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int AvailableSamples
    {
        get { lock (_lock) return _available; }
    }

    public TeeSampleProvider(ISampleProvider source, int bufferSize = 8192)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ringBuffer = new float[bufferSize];
    }

    public int Read(Span<float> buffer)
    {
        int samplesRead = _source.Read(buffer);

        if (samplesRead > 0)
        {
            lock (_lock)
            {
                int capacity = _ringBuffer.Length;
                for (int i = 0; i < samplesRead; i++)
                {
                    _ringBuffer[_writePos] = buffer[i];
                    _writePos = (_writePos + 1) % capacity;
                    if (_available < capacity)
                        _available++;
                    else
                        _readPos = (_readPos + 1) % capacity;
                }
            }
        }

        return samplesRead;
    }

    public int ReadTap(Span<float> buffer)
    {
        lock (_lock)
        {
            int toRead = Math.Min(buffer.Length, _available);
            for (int i = 0; i < toRead; i++)
            {
                buffer[i] = _ringBuffer[_readPos];
                _readPos = (_readPos + 1) % _ringBuffer.Length;
            }
            _available -= toRead;
            return toRead;
        }
    }

    public void Dispose()
    {
        if (_source is IDisposable disposable)
            disposable.Dispose();
    }
}