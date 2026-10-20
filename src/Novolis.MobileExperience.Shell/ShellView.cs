#if false
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Avalonia.Slate;
using Novolis.Markup.Slate;
using Profile = Novolis.Avalonia.GraphicalProfile.GraphicalProfile;

namespace Novolis.MobileExperience.Shell;

/// <summary>Slate-based tablet shell and host-owned Android application launcher.</summary>
public sealed class ShellView : UserControl
{
    private static readonly IReadOnlyList<SlateNavigationItem> NavigationItems =
    [
        new(SlateRoute.Home, "home", "⌂"),
        new(SlateRoute.Apps, "apps", "▦"),
        new(SlateRoute.System, "system", "⚙"),
        new(SlateRoute.Terminal, "terminal", "⌘"),
    ];

    private readonly IApplicationCatalog _catalog;
    private readonly SlateShell _slateShell = new();
    private readonly SlateAppCatalog _novolisApplications = new();
    private readonly SlateAppCatalog _otherApplications = new();
    private readonly SlateSystemMonitorPanel _systemMonitors = new();
    private readonly SlateTerminalSurface _terminal = new();
    private readonly TextBlock _status = new();
    private readonly Button _refreshButton;
    private IReadOnlyList<MobileApplicationEntry> _applications = [];
    private IReadOnlyList<MobileApplicationEntry> _launchableApplications = [];
    private bool _hasLoaded;

    /// <summary>Creates a Slate shell backed by a platform application catalog.</summary>
    public ShellView(IApplicationCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _refreshButton = CreateRefreshButton();

        _status.Text = "Ready to inspect installed applications.";
        _status.Classes.Add("body-copy");
        GraphicalProfileBinding.Bind(
            _status,
            TextBlock.ForegroundProperty,
            Profile.MutedResourceKey);

        _slateShell.Items = NavigationItems;
        _slateShell.RouteRequested += (_, request) => NavigateTo(request.Route);
        _novolisApplications.AppSelected += (_, application) =>
            _ = LaunchApplicationAsync(application);
        _otherApplications.AppSelected += (_, application) =>
            _ = LaunchApplicationAsync(application);
        _systemMonitors.ActionRequested += (_, monitor) =>
            _status.Text = $"{monitor.Label} is read-only in this shell.";

        Content = _slateShell;
        NavigateTo(SlateRoute.Home);
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        _ = RefreshApplicationsAsync();
    }

    private void NavigateTo(SlateRoute route)
    {
        _slateShell.SelectedRoute = route;
        _slateShell.Body = route switch
        {
            SlateRoute.Home => BuildHomeBody(),
            SlateRoute.Apps => BuildApplicationsBody(),
            SlateRoute.System => BuildSystemBody(),
            SlateRoute.Terminal => BuildTerminalBody(),
            _ => BuildHomeBody(),
        };
    }

    private Control BuildHomeBody()
    {
        var tiles = new SlateTileBoard
        {
            Items =
            [
                new SlateTileItem(
                    "apps",
                    "APPLICATIONS",
                    $"{_launchableApplications.Count} ready · Novolis first",
                    SlateRoute.Apps,
                    SlateTileTone.Primary,
                    2),
                new SlateTileItem(
                    "system",
                    "SYSTEM",
                    "Read-only privacy and device status",
                    SlateRoute.System,
                    SlateTileTone.Information),
                new SlateTileItem(
                    "terminal",
                    "MAINTENANCE",
                    "Read-only local diagnostics",
                    SlateRoute.Terminal,
                    SlateTileTone.Neutral),
            ],
        };
        tiles.TileInvoked += (_, tile) => NavigateTo(tile.TargetRoute);

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new SlatePageHeader
                {
                    Title = "Your Novolis tablet",
                    Eyebrow = "MOBILE EXPERIENCE · LOCAL FIRST",
                },
                BuildStatusCard(),
                tiles,
                CreateReadOnlyNote(
                    "Novolis is the primary surface. Android remains the compatibility layer beneath it."),
            },
        };
        return CreateScrollViewer(content);
    }

    private Control BuildApplicationsBody()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new SlatePageHeader
                {
                    Title = "Applications",
                    Eyebrow = "NOVOLIS FIRST · DISPLAY NAME ASCENDING",
                },
                BuildStatusCard(),
                BuildApplicationSection(
                    "NOVOLIS",
                    "Novolis applications are presented first.",
                    _novolisApplications,
                    _launchableApplications.Count(application => application.IsNovolisApplication)),
                BuildApplicationSection(
                    "OTHER APPS",
                    "Other installed Android applications remain available below.",
                    _otherApplications,
                    _launchableApplications.Count(application => !application.IsNovolisApplication)),
            },
        };
        return CreateScrollViewer(content);
    }

    private Control BuildSystemBody()
    {
        _systemMonitors.Items = CreateSystemMonitors();
        _systemMonitors.MinHeight = 420;

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new SlatePageHeader
                {
                    Title = "System status",
                    Eyebrow = "READ-ONLY · HOST AND DEVICE BOUNDARIES",
                },
                BuildStatusCard(),
                _systemMonitors,
                CreateReadOnlyNote(
                    "Provisioning and package policy remain outside the visual shell."),
            },
        };
        return CreateScrollViewer(content);
    }

    private Control BuildTerminalBody()
    {
        _terminal.Lines = CreateTerminalLines();

        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new SlatePageHeader
                {
                    Title = "Maintenance",
                    Eyebrow = "LOCAL TRANSCRIPT · COMMAND INPUT DISABLED",
                },
                BuildStatusCard(),
                _terminal,
            },
        };
        return CreateScrollViewer(content);
    }

    private Control BuildApplicationSection(
        string title,
        string subtitle,
        SlateAppCatalog catalog,
        int count)
    {
        var heading = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                CreateSectionLabel($"{title} · {count} READY"),
                CreateMutedText(subtitle),
            },
        };

        Control applicationSurface = count == 0
            ? CreateEmptyCatalogMessage(
                title == "NOVOLIS"
                    ? "No Novolis applications were reported."
                    : "No other launchable applications were reported.")
            : catalog;

        if (count > 0)
        {
            catalog.Height = Math.Max(
                156,
                Math.Ceiling(count / 2d) * 156 + 8);
        }

        return new StackPanel
        {
            Spacing = 10,
            Children = { heading, applicationSurface },
        };
    }

    private async Task RefreshApplicationsAsync()
    {
        if (!_refreshButton.IsEnabled)
            return;

        _refreshButton.IsEnabled = false;
        _status.Text = "Reading launchable applications…";
        try
        {
            _applications = await _catalog.DiscoverAsync().ConfigureAwait(true);
            _launchableApplications = _applications
                .Where(application => !application.IsHomeApplication)
                .ToArray();

            _novolisApplications.Apps = _launchableApplications
                .Where(application => application.IsNovolisApplication)
                .Select(ToSlateDescriptor)
                .ToArray();
            _otherApplications.Apps = _launchableApplications
                .Where(application => !application.IsNovolisApplication)
                .Select(ToSlateDescriptor)
                .ToArray();

            _status.Text = _launchableApplications.Count == 0
                ? "PackageManager returned no launchable applications."
                : $"{_launchableApplications.Count} launchable applications ready.";
            NavigateTo(_slateShell.SelectedRoute);
        }
        catch (Exception ex)
        {
            _applications = [];
            _launchableApplications = [];
            _novolisApplications.Apps = [];
            _otherApplications.Apps = [];
            _status.Text = $"Discovery failed; retry is available: {ex.Message}";
            NavigateTo(_slateShell.SelectedRoute);
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    private async Task LaunchApplicationAsync(SlateAppDescriptor descriptor)
    {
        var application = _launchableApplications.FirstOrDefault(
            candidate => string.Equals(
                candidate.PackageName,
                descriptor.Id,
                StringComparison.Ordinal));
        if (application is null || !application.CanOpen)
        {
            _status.Text = $"{descriptor.Name} is not available to open.";
            return;
        }

        _status.Text = $"Opening {descriptor.Name}…";
        try
        {
            var launched = await _catalog.LaunchAsync(application)
                .ConfigureAwait(true);
            _status.Text = launched
                ? $"Opened {descriptor.Name}."
                : $"Android declined the launch request for {descriptor.Name}.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open {descriptor.Name}: {ex.Message}";
        }
    }

    private IReadOnlyList<SlateMonitorItem> CreateSystemMonitors() =>
    [
        new(
            "network",
            "Update network",
            "Host-managed",
            "The shell has no network permission.",
            SlateMonitorSeverity.Healthy),
        new(
            "accounts",
            "Accounts",
            "Not queried",
            "Use the provisioning audit for device account state.",
            SlateMonitorSeverity.Neutral),
        new(
            "home",
            "Home surface",
            "Novolis",
            "This application owns the HOME activity.",
            SlateMonitorSeverity.Healthy),
        new(
            "applications",
            "Applications",
            _launchableApplications.Count.ToString(),
            "Launchable entries, sorted by display name.",
            SlateMonitorSeverity.Neutral),
    ];

    private IReadOnlyList<SlateTerminalLine> CreateTerminalLines() =>
    [
        new("nme> system info", SlateTerminalLineKind.Prompt),
        new(
            "PRODUCT  Novolis Mobile Experience\n"
            + "UX       Slate\n"
            + "MODE     Offline first\n"
            + $"APPS     {_launchableApplications.Count} launchable entries",
            SlateTerminalLineKind.Output),
        new(
            "COMMAND INPUT DISABLED · HOST MAINTENANCE ONLY",
            SlateTerminalLineKind.Muted),
    ];

    private static SlateAppDescriptor ToSlateDescriptor(
        MobileApplicationEntry application) =>
        new(
            application.PackageName,
            application.DisplayName,
            application.IsNovolisApplication ? "Novolis" : "Android",
            SlateApplicationState.Installed,
            application.PackageName,
            application.IsNovolisApplication ? "▣" : "●");

    private Control BuildStatusCard()
    {
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        CreateSectionLabel("SESSION STATUS"),
                        _status,
                    },
                },
                _refreshButton,
            },
        };
        Grid.SetColumn(_refreshButton, 1);
        _refreshButton.VerticalAlignment = VerticalAlignment.Center;

        var card = new Border
        {
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = layout,
        };
        GraphicalProfileBinding.Bind(
            card,
            Border.BackgroundProperty,
            Profile.RaisedResourceKey);
        GraphicalProfileBinding.Bind(
            card,
            Border.BorderBrushProperty,
            Profile.BorderResourceKey);
        return card;
    }

    private static ScrollViewer CreateScrollViewer(Control content) =>
        new()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
        };

    private static TextBlock CreateSectionLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
        };
        GraphicalProfileBinding.Bind(
            label,
            TextBlock.ForegroundProperty,
            Profile.MutedResourceKey);
        return label;
    }

    private static TextBlock CreateMutedText(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
        };
        GraphicalProfileBinding.Bind(
            label,
            TextBlock.ForegroundProperty,
            Profile.MutedResourceKey);
        return label;
    }

    private static Control CreateReadOnlyNote(string text)
    {
        var note = new Border
        {
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = CreateMutedText(text),
        };
        GraphicalProfileBinding.Bind(
            note,
            Border.BackgroundProperty,
            Profile.SurfaceResourceKey);
        GraphicalProfileBinding.Bind(
            note,
            Border.BorderBrushProperty,
            Profile.BorderResourceKey);
        return note;
    }

    private static Control CreateEmptyCatalogMessage(string text)
    {
        var card = new Border
        {
            MinHeight = 100,
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = CreateMutedText(text),
        };
        GraphicalProfileBinding.Bind(
            card,
            Border.BackgroundProperty,
            Profile.RaisedResourceKey);
        GraphicalProfileBinding.Bind(
            card,
            Border.BorderBrushProperty,
            Profile.BorderResourceKey);
        return card;
    }

    private Button CreateRefreshButton()
    {
        var button = new Button
        {
            Content = "REFRESH",
            MinWidth = 112,
            MinHeight = 48,
            Padding = new Thickness(13, 8),
            FontWeight = FontWeight.Bold,
        };
        button.Click += (_, _) => _ = RefreshApplicationsAsync();
        ToolTip.SetTip(button, "Refresh installed Android applications");
        AutomationProperties.SetName(
            button,
            "Refresh installed Android applications");
        GraphicalProfileBinding.Bind(
            button,
            Button.BackgroundProperty,
            Profile.AccentFillResourceKey);
        GraphicalProfileBinding.Bind(
            button,
            Button.ForegroundProperty,
            Profile.OnAccentFillResourceKey);
        return button;
    }
}
#endif

namespace Novolis.MobileExperience.Shell;

/// <summary>Compatibility name for hosts that still construct ShellView.</summary>
public sealed class ShellView : SlateExperienceView
{
    /// <summary>Creates the current Slate experience view.</summary>
    public ShellView(IApplicationCatalog catalog)
        : base(catalog)
    {
    }
}
