using System;
using System.IO;
using System.Text;

namespace ProjectVinyl.Services;

/// <summary>
/// Structured logging service for audio playback diagnostics.
/// Writes timestamped events to Executable/audio.log for post-mortem analysis.
/// Thread-safe: all writes are serialized via lock.
/// </summary>
internal static class AudioLogService
{
    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static string _logPath = string.Empty;

    public static void Initialize(string basePath)
    {
        lock (_lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _logPath = Path.Combine(basePath, "audio.log");
            _writer = new StreamWriter(_logPath, append: false, encoding: Encoding.UTF8)
            {
                AutoFlush = true
            };
            Write("LOG_INIT", $"Audio logging started at {basePath}");
        }
    }

    public static void Write(string category, string message)
    {
        lock (_lock)
        {
            if (_writer == null) return;
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer.WriteLine($"[{ts}] [{category}] {message}");
        }
    }

    public static void Flush()
    {
        lock (_lock)
        {
            _writer?.Flush();
        }
    }
}