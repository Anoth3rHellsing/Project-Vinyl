using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectVinyl.Models;
using ProjectVinyl.Services;

namespace ProjectVinyl.ViewModels;

/// <summary>
/// ViewModel dedicated to YouTube audio downloading (Vinyl Creator).
/// Extracted from MainViewModel to separate concerns.
/// Notifies parent via TrackDownloaded callback when a download completes.
/// </summary>
public partial class VinylCreatorViewModel : ViewModelBase
{
    private readonly YoutubeDownloadService _downloadService = new();
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private string _urlInput = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _statusMessage = "Paste a YouTube URL to download audio";

    /// <summary>
    /// Raised when a track has been successfully downloaded.
    /// Parent (MainViewModel) subscribes to refresh the library.
    /// </summary>
    public event Action<Track>? TrackDownloaded;

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(UrlInput)) return;

        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        IsDownloading = true;
        DownloadProgress = 0;
        StatusMessage = "Downloading...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() => DownloadProgress = p * 100);
            });
            var track = await _downloadService.DownloadAudioAsync(UrlInput.Trim(), progress, ct);

            TrackDownloaded?.Invoke(track);
            StatusMessage = $"Downloaded: {track.Title}";
            UrlInput = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            DownloadProgress = 0;
            _downloadCts = null;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    [RelayCommand]
    private void OpenLibraryFolder()
    {
        try
        {
            var path = _downloadService.LibraryPath;
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    private bool CanDownload() => !IsDownloading && !string.IsNullOrWhiteSpace(UrlInput);

    partial void OnUrlInputChanged(string value) => DownloadCommand.NotifyCanExecuteChanged();
}