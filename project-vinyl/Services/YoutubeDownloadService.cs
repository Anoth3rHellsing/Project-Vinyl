using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjectVinyl.Models;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace ProjectVinyl.Services;

public class YoutubeDownloadService
{
    private readonly YoutubeClient _youtube = new();
    private readonly string _libraryPath;

    public string LibraryPath => _libraryPath;

    public YoutubeDownloadService(string? libraryPath = null)
    {
        _libraryPath = libraryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "ProjectVinyl");
        Directory.CreateDirectory(_libraryPath);
    }

    public async Task<Track> DownloadAudioAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var video = await _youtube.Videos.GetAsync(url, ct);
        var manifest = await _youtube.Videos.Streams.GetManifestAsync(video.Id, ct);
        var audioStream = manifest.GetAudioStreams().GetWithHighestBitrate()
            ?? throw new InvalidOperationException("No audio stream found for this video.");

        // Include video ID in filename to prevent collisions between videos with same title
        var safeTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
        if (safeTitle.Length > 200)
            safeTitle = safeTitle[..200];
        var fileName = $"{safeTitle}_{video.Id.Value}.{audioStream.Container.Name}";
        var filePath = Path.Combine(_libraryPath, fileName);

        if (!File.Exists(filePath))
        {
            // Download to temp file first, then rename atomically to avoid partial files
            var tempPath = filePath + ".tmp";
            try
            {
                await _youtube.Videos.Streams.DownloadAsync(audioStream, tempPath, progress, ct);
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch
            {
                // Clean up partial temp file on failure
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* best effort */ }
                }
                throw;
            }
        }

        return new Track
        {
            Id = video.Id.Value,
            Title = video.Title,
            Artist = video.Author.ChannelTitle,
            Duration = video.Duration ?? TimeSpan.Zero,
            FilePath = filePath,
            CoverUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url ?? string.Empty,
            Genre = "Unknown",
            AddedAt = DateTime.UtcNow
        };
    }

    public IReadOnlyList<Track> GetLocalLibrary()
    {
        var tracks = new List<Track>();
        if (!Directory.Exists(_libraryPath)) return tracks;

        foreach (var file in Directory.GetFiles(_libraryPath))
        {
            try
            {
                using var tag = TagLib.File.Create(file);
                var genres = tag.Tag.Genres;
                var genre = (genres != null && genres.Length > 0 && !string.IsNullOrEmpty(genres[0]))
                    ? genres[0]
                    : "Unknown";

                tracks.Add(new Track
                {
                    Id = Path.GetFileNameWithoutExtension(file),
                    Title = string.IsNullOrEmpty(tag.Tag.Title) ? Path.GetFileNameWithoutExtension(file) : tag.Tag.Title,
                    Artist = string.IsNullOrEmpty(tag.Tag.FirstPerformer) ? "Unknown Artist" : tag.Tag.FirstPerformer,
                    Album = tag.Tag.Album ?? string.Empty,
                    Duration = tag.Properties.Duration,
                    FilePath = file,
                    Genre = genre,
                    AddedAt = File.GetCreationTimeUtc(file)
                });
            }
            catch
            {
                // Skip files that can't be read as audio
            }
        }

        return tracks.OrderByDescending(t => t.AddedAt).ToList();
    }
}