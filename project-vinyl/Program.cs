using Avalonia;
using System;
using System.IO;
using ProjectVinyl.Services;

namespace ProjectVinyl;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize audio logging to Executable folder (next to the exe)
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
        AudioLogService.Initialize(exeDir);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        AudioLogService.Write("APP_EXIT", "Application shutting down");
        AudioLogService.Flush();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
// Developer tools removed — package not referenced in this configuration
            .WithInterFont()
            .LogToTrace();
}
