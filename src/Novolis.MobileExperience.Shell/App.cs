using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.MobileExperience.Shell;

/// <summary>Application root for the Novolis Mobile Experience shell.</summary>
public sealed class App : Application
{
    /// <summary>Platform catalog assigned before Avalonia starts.</summary>
    public static IApplicationCatalog ApplicationCatalog { get; set; } =
        new EmptyApplicationCatalog();

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var mainView = new ShellView(ApplicationCatalog);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShellWindow
            {
                Content = mainView,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
