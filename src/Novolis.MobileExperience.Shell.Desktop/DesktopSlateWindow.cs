using Avalonia.Controls;

namespace Novolis.MobileExperience.Shell.Desktop;

/// <summary>Resizable tablet-shaped window for Slate visual experiments.</summary>
public sealed class DesktopSlateWindow : Window
{
    /// <summary>Creates a window using the requested viewport dimensions.</summary>
    public DesktopSlateWindow(SlateDesktopOptions options)
    {
        Title = "Novolis Slate";
        Width = options.Width;
        Height = options.Height;
        MinWidth = 320;
        MinHeight = 480;
    }
}
