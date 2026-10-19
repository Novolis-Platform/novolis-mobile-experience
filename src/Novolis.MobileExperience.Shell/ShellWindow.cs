using Avalonia.Controls;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.MobileExperience.Shell;

/// <summary>Desktop host window for shell development and inspection.</summary>
public sealed class ShellWindow : Window
{
    /// <summary>Creates the shell development window.</summary>
    public ShellWindow()
    {
        Title = "Novolis Mobile Experience";
        Width = 1180;
        Height = 760;
        MinWidth = 720;
        MinHeight = 520;
        GraphicalProfile.ApplyWindowChrome(this);
    }
}
