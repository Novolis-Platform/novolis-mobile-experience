using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.MobileExperience.Shell;

/// <summary>Application root for the Novolis Mobile Experience shell.</summary>
public sealed class App : Application
{
    private static SlateExperienceView? _mainView;

    /// <summary>Platform catalog assigned before Avalonia starts.</summary>
    public static IApplicationCatalog ApplicationCatalog { get; set; } =
        new EmptyApplicationCatalog();

    /// <summary>Platform capabilities assigned by the Android host before Avalonia starts.</summary>
    public static IMobileExperienceServices ExperienceServices { get; set; } =
        EmptyMobileExperienceServices.Instance;

    /// <summary>Applies activity-bound capabilities to an already-created shell.</summary>
    public static void ConfigureExperienceServices(IMobileExperienceServices services)
    {
        ExperienceServices = services ?? throw new ArgumentNullException(nameof(services));
        _mainView?.ConfigureServices(services);
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        GraphicalProfile.Install(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var mainView = new SlateExperienceView(
            ApplicationCatalog,
            services: ExperienceServices);
        _mainView = mainView;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShellWindow
            {
                Content = mainView,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new Border
            {
                Background = SlateVisualTokens.Brush(SlateVisualTokens.Background),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = mainView,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Shows Slate quick actions after Android HOME returns to this launcher.</summary>
    public static void NotifySystemHomePressed()
    {
        _mainView?.RefreshPlatformState();
        _mainView?.ShowQuickActions();
    }
}
