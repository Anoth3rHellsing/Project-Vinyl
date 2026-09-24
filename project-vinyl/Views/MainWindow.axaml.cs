using Avalonia.Controls;
using Avalonia.Input;
using ProjectVinyl.ViewModels;

namespace ProjectVinyl.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSeekPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.BeginUserSeek();
    }

    private void OnSeekPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.EndUserSeek();
    }
}