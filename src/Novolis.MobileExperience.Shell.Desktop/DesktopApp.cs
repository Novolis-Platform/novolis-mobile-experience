using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Desktop;

/// <summary>Avalonia application root for the Windows Slate visual prototype.</summary>
public sealed class DesktopApp : Application
{
    /// <summary>Options supplied to the current prototype process.</summary>
    public static SlateDesktopOptions Options { get; set; } =
        new(1920, 1200, SlateNavigationEdge.Automatic);

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var view = new SlateExperienceView(
            new DesktopDemoApplicationCatalog(),
            Options.Navigation);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new DesktopSlateWindow(Options)
            {
                Content = view,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
