using System;
using NAudio.Wave;

namespace ProjectVinyl.Services;

public class AudioVisualizerService : IDisposable
{
    private const int FftSize = 1024;
    private const int BandCount = 64;
    private const float SmoothingFactor = 0.3f;

    private readonly float[] _fftBuffer = new float[FftSize];
    private readonly float[] _bands = new float[BandCount];
    private readonly float[] _smoothedBands = new float[BandCount];
    private readonly object _lock = new();

    // Precomputed Hann window to avoid per-frame allocation
    private readonly float[] _hannWindow;

    private ISampleProvider? _sampleProvider;
    private bool _isActive;
    private bool _disposed;

    public int BandCountValue => BandCount;

    public AudioVisualizerService()
    {
        _hannWindow = new float[FftSize];
        for (int i = 0; i < FftSize; i++)
        {
            _hannWindow[i] = 0.5f * (1f - (float)Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));
        }
    }

    public void Attach(ISampleProvider sampleProvider)
    {
        lock (_lock)
        {
            _sampleProvider = sampleProvider;
            _isActive = true;
            Array.Clear(_smoothedBands, 0, _smoothedBands.Length);
            Array.Clear(_bands, 0, _bands.Length);
        }
    }

    public void Detach()
    {
        lock (_lock)
        {
            _sampleProvider = null;
            _isActive = false;
            Array.Clear(_smoothedBands, 0, _smoothedBands.Length);
            Array.Clear(_bands, 0, _bands.Length);
        }
    }

    /// <summary>
    /// Returns current frequency band amplitudes (0..1), smoothed.
    /// Safe to call from the render thread. Reads samples non-destructively
    /// from the tapped sample provider.
    /// </summary>
    public ReadOnlySpan<float> GetBands()
    {
        lock (_lock)
        {
            if (!_isActive || _sampleProvider == null)
            {
                return _smoothedBands.AsSpan();
            }

            int samplesRead = _sampleProvider.Read(_fftBuffer.AsSpan(0, FftSize));

            if (samplesRead < FftSize)
            {
                for (int i = samplesRead; i < FftSize; i++)
                {
                    _fftBuffer[i] = 0f;
                }
            }

            ComputeFftMagnitudes();
            MapToBands();
            ApplySmoothing();

            return _smoothedBands.AsSpan();
        }
    }

    private void ComputeFftMagnitudes()
    {
        // Apply Hann window in-place
        for (int i = 0; i < FftSize; i++)
        {
            _fftBuffer[i] *= _hannWindow[i];
        }

        // In-place radix-2 Cooley-Tukey FFT (real input, complex output stored interleaved)
        // We reuse _fftBuffer as the real part and use a separate imaginary array on stack-friendly basis.
        // Since we only need magnitudes of the first half, this is efficient enough.
        Span<float> real = _fftBuffer.AsSpan();
        Span<float> imag = new float[FftSize]; // zero-initialized

        FftRadix2(real, imag, FftSize);

        // Compute magnitudes for first half (Nyquist)
        int halfSize = FftSize / 2;
        float norm = 2f / FftSize;
        for (int k = 0; k < halfSize; k++)
        {
            float r = real[k];
            float im = imag[k];
            _fftBuffer[k] = (float)Math.Sqrt(r * r + im * im) * norm;
        }
    }

    private static void FftRadix2(Span<float> real, Span<float> imag, int n)
    {
        // Bit-reversal permutation
        int bits = 0;
        while ((1 << bits) < n) bits++;

        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        // Cooley-Tukey butterfly
        for (int size = 2; size <= n; size *= 2)
        {
            int halfSize = size / 2;
            double angleStep = -2.0 * Math.PI / size;

            for (int i = 0; i < n; i += size)
            {
                for (int k = 0; k < halfSize; k++)
                {
                    double angle = angleStep * k;
                    float wr = (float)Math.Cos(angle);
                    float wi = (float)Math.Sin(angle);

                    int evenIdx = i + k;
                    int oddIdx = i + k + halfSize;

                    float tr = wr * real[oddIdx] - wi * imag[oddIdx];
                    float ti = wr * imag[oddIdx] + wi * real[oddIdx];

                    real[oddIdx] = real[evenIdx] - tr;
                    imag[oddIdx] = imag[evenIdx] - ti;
                    real[evenIdx] += tr;
                    imag[evenIdx] += ti;
                }
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    private void MapToBands()
    {
        int halfSize = FftSize / 2;

        // Logarithmic frequency mapping for perceptual relevance
        for (int i = 0; i < BandCount; i++)
        {
            float lowFrac = (float)i / BandCount;
            float highFrac = (float)(i + 1) / BandCount;

            // Log-scale mapping — concentrates lower frequencies where human hearing is more sensitive
            int lowBin = (int)(Math.Pow(lowFrac, 2) * halfSize);
            int highBin = (int)(Math.Pow(highFrac, 2) * halfSize);

            if (highBin <= lowBin) highBin = lowBin + 1;
            if (highBin > halfSize) highBin = halfSize;

            float sum = 0f;
            int count = 0;
            for (int b = lowBin; b < highBin; b++)
            {
                sum += _fftBuffer[b];
                count++;
            }

            _bands[i] = count > 0 ? sum / count : 0f;
        }

        // Normalize to 0-1 range with headroom
        float max = 0f;
        for (int i = 0; i < BandCount; i++)
        {
            if (_bands[i] > max) max = _bands[i];
        }

        if (max > 0.001f)
        {
            float scale = 1f / (max * 1.2f);
            for (int i = 0; i < BandCount; i++)
            {
                _bands[i] = Math.Min(_bands[i] * scale, 1f);
            }
        }
    }

    private void ApplySmoothing()
    {
        for (int i = 0; i < BandCount; i++)
        {
            // Fast attack, slow decay for classic Winamp-style look
            if (_bands[i] > _smoothedBands[i])
            {
                _smoothedBands[i] = _bands[i];
            }
            else
            {
                _smoothedBands[i] = _smoothedBands[i] * (1f - SmoothingFactor) + _bands[i] * SmoothingFactor;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Detach();
        }
        GC.SuppressFinalize(this);
    }
}