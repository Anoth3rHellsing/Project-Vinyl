using System;
using NAudio.Wave;

namespace ProjectVinyl.Services;

/// <summary>
/// Schroeder-style reverb effect using 4 comb filters + 2 allpass filters.
/// Implements ISampleProvider for chainable DSP pipeline.
/// When WetDryMix = 0, acts as zero-cost passthrough.
/// </summary>
internal sealed class ReverbEffect : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private readonly int _channels;

    // Comb filter delays (in samples at 44100Hz, scaled to actual sample rate)
    private static readonly int[] BaseCombDelays = { 1557, 1617, 1491, 1422 };
    private static readonly int[] AllpassDelays = { 225, 556 };

    private float[][] _combBuffers = [];
    private int[] _combIndices = [];
    private float[] _combFeedbackGains = [];
    private float[][] _allpassBuffers = [];
    private int[] _allpassIndices = [];

    private float _wetDryMix;
    private float _decayTime;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float WetDryMix
    {
        get => _wetDryMix;
        set => _wetDryMix = Math.Clamp(value, 0f, 1f);
    }

    public float DecayTime
    {
        get => _decayTime;
        set
        {
            _decayTime = Math.Clamp(value, 0.1f, 5f);
            UpdateFeedbackGains();
        }
    }

    public ReverbEffect(ISampleProvider source, float decayTime = 1.5f)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sampleRate = source.WaveFormat.SampleRate;
        _channels = source.WaveFormat.Channels;
        _decayTime = decayTime;
        _wetDryMix = 0f; // Disabled by default

        InitializeBuffers();
        UpdateFeedbackGains();
    }

    private void InitializeBuffers()
    {
        double scaleFactor = _sampleRate / 44100.0;

        _combBuffers = new float[BaseCombDelays.Length][];
        _combIndices = new int[BaseCombDelays.Length];
        _combFeedbackGains = new float[BaseCombDelays.Length];

        for (int i = 0; i < BaseCombDelays.Length; i++)
        {
            int delay = (int)(BaseCombDelays[i] * scaleFactor);
            if (delay < 1) delay = 1;
            _combBuffers[i] = new float[delay * _channels];
            _combIndices[i] = 0;
        }

        _allpassBuffers = new float[AllpassDelays.Length][];
        _allpassIndices = new int[AllpassDelays.Length];

        for (int i = 0; i < AllpassDelays.Length; i++)
        {
            int delay = (int)(AllpassDelays[i] * scaleFactor);
            if (delay < 1) delay = 1;
            _allpassBuffers[i] = new float[delay * _channels];
            _allpassIndices[i] = 0;
        }
    }

    private void UpdateFeedbackGains()
    {
        // Calculate feedback gain based on decay time and comb delay
        for (int i = 0; i < BaseCombDelays.Length; i++)
        {
            double delaySeconds = BaseCombDelays[i] / 44100.0;
            // Gain so that signal decays to -60dB in DecayTime seconds
            float gain = (float)Math.Pow(0.001, delaySeconds / _decayTime);
            _combFeedbackGains[i] = Math.Clamp(gain, 0f, 0.98f);
        }
    }

    public int Read(Span<float> buffer)
    {
        int samplesRead = _source.Read(buffer);

        if (_wetDryMix <= 0.001f || samplesRead == 0)
            return samplesRead;

        // Process in-place: mix wet signal with dry
        int frames = samplesRead / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = frame * _channels + ch;
                float dry = buffer[idx];

                // Sum output from all comb filters
                float combSum = 0f;
                for (int c = 0; c < _combBuffers.Length; c++)
                {
                    int bufLen = _combBuffers[c].Length;
                    int readPos = _combIndices[c] + ch;
                    if (readPos >= bufLen) readPos -= bufLen;

                    float delayed = _combBuffers[c][readPos];
                    combSum += delayed;

                    // Write input + feedback into comb buffer
                    int writePos = _combIndices[c] + ch;
                    if (writePos >= bufLen) writePos -= bufLen;
                    _combBuffers[c][writePos] = dry + delayed * _combFeedbackGains[c];
                }

                // Advance comb indices (once per frame, not per channel)
                if (ch == _channels - 1)
                {
                    for (int c = 0; c < _combBuffers.Length; c++)
                    {
                        _combIndices[c] += _channels;
                        if (_combIndices[c] >= _combBuffers[c].Length)
                            _combIndices[c] = 0;
                    }
                }

                // Pass through allpass filters
                float apInput = combSum / _combBuffers.Length;
                float apOutput = apInput;

                for (int a = 0; a < _allpassBuffers.Length; a++)
                {
                    int bufLen = _allpassBuffers[a].Length;
                    int readPos = _allpassIndices[a] + ch;
                    if (readPos >= bufLen) readPos -= bufLen;

                    float delayed = _allpassBuffers[a][readPos];
                    float apOut = -apOutput * 0.5f + delayed;
                    _allpassBuffers[a][readPos] = apOutput + delayed * 0.5f;
                    apOutput = apOut;

                    if (ch == _channels - 1)
                    {
                        _allpassIndices[a] += _channels;
                        if (_allpassIndices[a] >= _allpassBuffers[a].Length)
                            _allpassIndices[a] = 0;
                    }
                }

                // Mix wet/dry
                buffer[idx] = dry * (1f - _wetDryMix) + apOutput * _wetDryMix;
            }
        }

        return samplesRead;
    }
}

/// <summary>
/// Bass boost effect using a second-order IIR low-shelf biquad filter.
/// Direct Form II transposed implementation with separate state per channel.
/// When GainDb = 0, acts as zero-cost passthrough.
/// </summary>
internal sealed class BassBoostEffect : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private readonly int _channels;

    // Biquad coefficients
    private float _b0, _b1, _b2, _a1, _a2;

    // Filter state per channel (Direct Form II transposed needs 2 state vars per channel)
    private float[] _state1;
    private float[] _state2;

    private float _gainDb;
    private float _cutoffHz;
    private bool _coefficientsDirty;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public float GainDb
    {
        get => _gainDb;
        set
        {
            var clamped = Math.Clamp(value, -12f, 12f);
            if (Math.Abs(_gainDb - clamped) > 0.01f)
            {
                _gainDb = clamped;
                _coefficientsDirty = true;
            }
        }
    }

    public float CutoffHz
    {
        get => _cutoffHz;
        set
        {
            var clamped = Math.Clamp(value, 60f, 200f);
            if (Math.Abs(_cutoffHz - clamped) > 0.1f)
            {
                _cutoffHz = clamped;
                _coefficientsDirty = true;
            }
        }
    }

    public BassBoostEffect(ISampleProvider source, float gainDb = 0f, float cutoffHz = 100f)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _sampleRate = source.WaveFormat.SampleRate;
        _channels = source.WaveFormat.Channels;
        _gainDb = gainDb;
        _cutoffHz = cutoffHz;
        _coefficientsDirty = true;

        _state1 = new float[_channels];
        _state2 = new float[_channels];

        ComputeCoefficients();
    }

    private void ComputeCoefficients()
    {
        if (!_coefficientsDirty) return;
        _coefficientsDirty = false;

        // Low-shelf filter design (RBJ Audio EQ Cookbook)
        float A = (float)Math.Pow(10.0, _gainDb / 40.0); // sqrt of linear gain
        float w0 = 2f * MathF.PI * _cutoffHz / _sampleRate;
        float sinW0 = MathF.Sin(w0);
        float cosW0 = MathF.Cos(w0);
        float alpha = sinW0 / 2f * MathF.Sqrt((A + 1f / A) * 2f - 2f); // Q ~ 0.707 equivalent

        float a0Inv = 1f / ((A + 1f) + (A - 1f) * cosW0 + 2f * MathF.Sqrt(A) * alpha);

        _b0 = (A * ((A + 1f) - (A - 1f) * cosW0 + 2f * MathF.Sqrt(A) * alpha)) * a0Inv;
        _b1 = (2f * A * ((A - 1f) - (A + 1f) * cosW0)) * a0Inv;
        _b2 = (A * ((A + 1f) - (A - 1f) * cosW0 - 2f * MathF.Sqrt(A) * alpha)) * a0Inv;
        _a1 = (-2f * ((A - 1f) + (A + 1f) * cosW0)) * a0Inv;
        _a2 = ((A + 1f) + (A - 1f) * cosW0 - 2f * MathF.Sqrt(A) * alpha) * a0Inv;
    }

    public int Read(Span<float> buffer)
    {
        int samplesRead = _source.Read(buffer);

        // Passthrough when gain is effectively zero
        if (Math.Abs(_gainDb) < 0.01f || samplesRead == 0)
            return samplesRead;

        ComputeCoefficients();

        int frames = samplesRead / _channels;

        for (int frame = 0; frame < frames; frame++)
        {
            for (int ch = 0; ch < _channels; ch++)
            {
                int idx = frame * _channels + ch;
                float input = buffer[idx];

                // Direct Form II Transposed
                float output = _b0 * input + _state1[ch];
                _state1[ch] = _b1 * input - _a1 * output + _state2[ch];
                _state2[ch] = _b2 * input - _a2 * output;

                // Denormal protection: flush subnormal state values to zero
                // Subnormals cause severe CPU stalls on x86/x64 → buffer underruns → crackling
                const float DenormalThreshold = 1e-15f;
                if (MathF.Abs(_state1[ch]) < DenormalThreshold) _state1[ch] = 0f;
                if (MathF.Abs(_state2[ch]) < DenormalThreshold) _state2[ch] = 0f;

                // Clamp to prevent NaN/Infinity and overflow
                if (float.IsNaN(output) || float.IsInfinity(output))
                    output = 0f;

                buffer[idx] = Math.Clamp(output, -1.5f, 1.5f);
            }
        }

        return samplesRead;
    }
}