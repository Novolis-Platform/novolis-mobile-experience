using Avalonia;

namespace Novolis.MobileExperience.Shell.Desktop;

/// <summary>Starts the Windows Slate visual prototype.</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        DesktopApp.Options = SlateDesktopOptions.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<DesktopApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
