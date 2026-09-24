using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectVinyl.Models;
using ProjectVinyl.Services;

namespace ProjectVinyl.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AudioPlayerService _audioPlayer = new();
    private readonly AudioVisualizerService _visualizerService = new();
    private readonly LibraryFilterService _filterService = new();
    private readonly VinylCreatorViewModel _creatorVm = new();
    private readonly AboutViewModel _aboutVm = new();
    private bool _isUserSeeking;

    // --- Navigation ---
    [ObservableProperty]
    private object _currentView;

    [ObservableProperty]
    private bool _isLibraryActive = true;

    [ObservableProperty]
    private bool _isCreatorActive;

    // --- Library State ---
    [ObservableProperty]
    private Track? _selectedTrack;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string? _selectedFilterValue;

    public ObservableCollection<Track> Library { get; } = new();
    public ObservableCollection<string> FilterOptions { get; } = new();
    public ObservableCollection<Track> FilteredLibrary { get; } = new();

    // --- Playback State ---
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    private TimeSpan _totalDuration;

    [ObservableProperty]
    private double _volume = 0.5;

    [ObservableProperty]
    private double _seekPositionPercent;

    [ObservableProperty]
    private double _playbackSpeedPercent = 100;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // --- DSP State ---
    [ObservableProperty]
    private bool _isReverbEnabled;

    [ObservableProperty]
    private double _reverbMix = 0.3;

    [ObservableProperty]
    private bool _isBassBoostEnabled;

    [ObservableProperty]
    private double _bassBoostGain = 6.0;

    public AudioVisualizerService VisualizerService => _visualizerService;
    public VinylCreatorViewModel CreatorViewModel => _creatorVm;

    public MainViewModel()
    {
        _currentView = this; // Default to library view (self-bound via DataTemplate)

        _audioPlayer.PositionChanged += OnPositionChanged;
        _audioPlayer.PlaybackStopped += OnPlaybackStopped;
        _audioPlayer.SampleProviderChanged += OnSampleProviderChanged;

        _creatorVm.TrackDownloaded += OnTrackDownloaded;

        LoadLibrary();
    }

    // --- Navigation Commands ---
    [RelayCommand]
    private void NavigateToLibrary()
    {
        CurrentView = this;
        IsLibraryActive = true;
        IsCreatorActive = false;
    }

    [RelayCommand]
    private void NavigateToCreator()
    {
        CurrentView = _creatorVm;
        IsLibraryActive = false;
        IsCreatorActive = true;
    }

    [RelayCommand]
    private void NavigateToAbout()
    {
        CurrentView = _aboutVm;
        IsLibraryActive = false;
        IsCreatorActive = false;
    }

    // --- DSP Commands/Handlers ---
    partial void OnIsReverbEnabledChanged(bool value)
    {
        _audioPlayer.IsReverbEnabled = value;
    }

    partial void OnReverbMixChanged(double value)
    {
        _audioPlayer.ReverbMix = value;
    }

    partial void OnIsBassBoostEnabledChanged(bool value)
    {
        _audioPlayer.IsBassBoostEnabled = value;
    }

    partial void OnBassBoostGainChanged(double value)
    {
        _audioPlayer.BassBoostGain = value;
    }

    // --- Library Management ---
    private void OnTrackDownloaded(Track track)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existingIndex = -1;
            for (int i = 0; i < Library.Count; i++)
            {
                if (Library[i].Id == track.Id)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
                Library[existingIndex] = track;
            else
                Library.Insert(0, track);

            RefreshFilterOptions();
            RefreshFilteredLibrary();
            StatusMessage = $"Added: {track.Title}";
        });
    }

    private void LoadLibrary()
    {
        var downloadService = new YoutubeDownloadService();
        var tracks = downloadService.GetLocalLibrary();
        Library.Clear();
        foreach (var track in tracks)
            Library.Add(track);

        RefreshFilterOptions();
        RefreshFilteredLibrary();
        StatusMessage = $"{Library.Count} tracks in library";
    }

    private void RefreshFilterOptions()
    {
        FilterOptions.Clear();
        switch (SelectedCategory)
        {
            case "Artists":
                foreach (var item in _filterService.GetArtists(Library))
                    FilterOptions.Add(item);
                break;
            case "Albums":
                foreach (var item in _filterService.GetAlbums(Library))
                    FilterOptions.Add(item);
                break;
            case "Genres":
                foreach (var item in _filterService.GetGenres(Library))
                    FilterOptions.Add(item);
                break;
        }
    }

    private void RefreshFilteredLibrary()
    {
        FilteredLibrary.Clear();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var results = _filterService.SearchTracks(Library, SearchQuery);
            foreach (var track in results)
                FilteredLibrary.Add(track);
            return;
        }

        if (SelectedCategory == "All" || SelectedFilterValue == null)
        {
            foreach (var track in Library)
                FilteredLibrary.Add(track);
            return;
        }

        var filtered = SelectedCategory switch
        {
            "Artists" => _filterService.FilterByArtist(Library, SelectedFilterValue),
            "Albums" => _filterService.FilterByAlbum(Library, SelectedFilterValue),
            "Genres" => _filterService.FilterByGenre(Library, SelectedFilterValue),
            _ => Library.AsEnumerable()
        };

        foreach (var track in filtered)
            FilteredLibrary.Add(track);
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
        SelectedFilterValue = null;
        SearchQuery = string.Empty;
        RefreshFilterOptions();
        RefreshFilteredLibrary();
    }

    [RelayCommand]
    private void SelectFilter(string value)
    {
        SelectedFilterValue = value;
        RefreshFilteredLibrary();
    }

    [RelayCommand]
    private void Search()
    {
        SelectedFilterValue = null;
        RefreshFilteredLibrary();
    }

    partial void OnSearchQueryChanged(string value) => SearchCommand.Execute(null);

    // --- Playback Controls ---
    partial void OnPlaybackSpeedPercentChanged(double value)
    {
        var rate = (float)(value / 100.0);
        _audioPlayer.ChangePlaybackRate(rate);
    }

    partial void OnVolumeChanged(double value)
    {
        _audioPlayer.Volume = value;
    }

    partial void OnSelectedTrackChanged(Track? value)
    {
        if (value != null)
            PlayTrack(value);
    }

    partial void OnSeekPositionPercentChanged(double value)
    {
        if (_isUserSeeking && TotalDuration.TotalMilliseconds > 0)
        {
            var targetPosition = TimeSpan.FromMilliseconds(
                TotalDuration.TotalMilliseconds * value / 100.0);
            _audioPlayer.Seek(targetPosition);
        }
    }

    public void BeginUserSeek() => _isUserSeeking = true;
    public void EndUserSeek() => _isUserSeeking = false;

    private void PlayTrack(Track track)
    {
        try
        {
            _audioPlayer.Play(track);
            IsPlaying = true;
            TotalDuration = _audioPlayer.TotalDuration;
            CurrentPosition = TimeSpan.Zero;
            SeekPositionPercent = 0;
            StatusMessage = $"Playing: {track.Title}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playback error: {ex.Message}";
            IsPlaying = false;
        }
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            _audioPlayer.Pause();
            IsPlaying = false;
            StatusMessage = "Paused";
        }
        else
        {
            if (SelectedTrack != null)
            {
                if (_audioPlayer.CurrentPosition == TimeSpan.Zero ||
                    _audioPlayer.CurrentPosition >= _audioPlayer.TotalDuration)
                {
                    PlayTrack(SelectedTrack);
                }
                else
                {
                    _audioPlayer.Resume();
                    IsPlaying = true;
                    StatusMessage = $"Playing: {SelectedTrack.Title}";
                }
            }
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (SelectedTrack == null || FilteredLibrary.Count == 0) return;

        if (FilteredLibrary.Count == 1)
        {
            PlayTrack(FilteredLibrary[0]);
            return;
        }

        var currentIndex = FilteredLibrary.IndexOf(SelectedTrack);
        SelectedTrack = currentIndex > 0
            ? FilteredLibrary[currentIndex - 1]
            : FilteredLibrary[FilteredLibrary.Count - 1];
    }

    [RelayCommand]
    private void Next()
    {
        if (SelectedTrack == null || FilteredLibrary.Count == 0) return;

        if (FilteredLibrary.Count == 1)
        {
            PlayTrack(FilteredLibrary[0]);
            return;
        }

        var currentIndex = FilteredLibrary.IndexOf(SelectedTrack);
        SelectedTrack = currentIndex < FilteredLibrary.Count - 1
            ? FilteredLibrary[currentIndex + 1]
            : FilteredLibrary[0];
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayer.Stop();
        IsPlaying = false;
        CurrentPosition = TimeSpan.Zero;
        SeekPositionPercent = 0;
        StatusMessage = "Stopped";
    }

    // --- Event Handlers ---
    private void OnSampleProviderChanged(NAudio.Wave.ISampleProvider? provider)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (provider != null)
                _visualizerService.Attach(provider);
            else
                _visualizerService.Detach();
        });
    }

    private void OnPositionChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentPosition = _audioPlayer.CurrentPosition;
            if (TotalDuration.TotalMilliseconds > 0)
            {
                _isUserSeeking = false;
                SeekPositionPercent = _audioPlayer.CurrentPosition.TotalMilliseconds
                                      / TotalDuration.TotalMilliseconds * 100.0;
            }
        });
    }

    private void OnPlaybackStopped()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = false;
            CurrentPosition = TimeSpan.Zero;
            SeekPositionPercent = 0;
            StatusMessage = "Playback finished";
        });
    }
}