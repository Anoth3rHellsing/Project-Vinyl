using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVinyl.Models;

namespace ProjectVinyl.Services;

public class LibraryFilterService
{
    public IReadOnlyList<string> GetArtists(IEnumerable<Track> tracks)
    {
        if (tracks == null) return Array.Empty<string>();
        return tracks
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Artist))
            .Select(t => t.Artist)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetAlbums(IEnumerable<Track> tracks)
    {
        if (tracks == null) return Array.Empty<string>();
        return tracks
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Album))
            .Select(t => t.Album)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetGenres(IEnumerable<Track> tracks)
    {
        if (tracks == null) return Array.Empty<string>();
        return tracks
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Genre))
            .Select(t => t.Genre)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<Track> FilterByArtist(IEnumerable<Track> tracks, string artist)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(artist)) return Array.Empty<Track>();
        return tracks
            .Where(t => t != null && string.Equals(t.Artist, artist, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<Track> FilterByAlbum(IEnumerable<Track> tracks, string album)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(album)) return Array.Empty<Track>();
        return tracks
            .Where(t => t != null && string.Equals(t.Album, album, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<Track> FilterByGenre(IEnumerable<Track> tracks, string genre)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(genre)) return Array.Empty<Track>();
        return tracks
            .Where(t => t != null && string.Equals(t.Genre, genre, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<Track> SearchTracks(IEnumerable<Track> tracks, string query)
    {
        if (tracks == null || string.IsNullOrWhiteSpace(query)) return Array.Empty<Track>();
        var q = query.Trim();
        return tracks
            .Where(t => t != null && (
                (t.Title != null && t.Title.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.Artist != null && t.Artist.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (t.Album != null && t.Album.Contains(q, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }
}