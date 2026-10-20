using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Novolis.MobileExperience.Shell;

/// <summary>Slate tablet shell with a functional command rail and live information tiles.</summary>
public class SlateExperienceView : UserControl
{
    private readonly IApplicationCatalog _catalog;
    private IMobileExperienceServices _services;
    private readonly SlateNavigationEdge _navigationPreference;
    private string _statusMessage = "Ready to observe the tablet.";
    private string _terminalTranscript = string.Empty;
    private IReadOnlyList<MobileApplicationEntry> _applications = [];
    private IReadOnlyList<MobileApplicationEntry> _launchableApplications = [];
    private BluetoothSnapshot _bluetoothSnapshot =
        BluetoothSnapshot.Unavailable("Bluetooth state has not been queried.");
    private MobileApplicationEntry? _selectedApplication;
    private string _route = "home";
    private SlateNavigationEdge _activeEdge;
    private double _lastLayoutWidth;
    private bool _hasLoaded;
    private bool _rebuilding;
    private bool _refreshInProgress;
    private bool _capabilityRefreshInProgress;
    private bool _quickActionsVisible;

    /// <summary>Creates a Slate shell backed by a platform application catalog.</summary>
    public SlateExperienceView(
        IApplicationCatalog catalog,
        SlateNavigationEdge navigation = SlateNavigationEdge.Automatic,
        IMobileExperienceServices? services = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _services = services ?? EmptyMobileExperienceServices.Instance;
        _navigationPreference = navigation;
        SizeChanged += OnSizeChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        RebuildLayout();
    }

    /// <summary>Replaces the platform capability set after the host activity is ready.</summary>
    public void ConfigureServices(IMobileExperienceServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        RebuildLayout();
        if (_hasLoaded)
            _ = RefreshCapabilitiesAsync();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        _ = RefreshApplicationsAsync();
        _ = RefreshCapabilitiesAsync();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (_rebuilding)
            return;

        var edge = ResolveNavigationEdge(Bounds.Width, Bounds.Height);
        if (edge != _activeEdge || Math.Abs(Bounds.Width - _lastLayoutWidth) > 96)
            RebuildLayout();
    }

    private void RebuildLayout()
    {
        _rebuilding = true;
        try
        {
            _activeEdge = ResolveNavigationEdge(Bounds.Width, Bounds.Height);
            _lastLayoutWidth = Bounds.Width;

            var body = new ContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = BuildRouteBody(),
            };
            Content = BuildRoot(body);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private SlateNavigationEdge ResolveNavigationEdge(double width, double height)
    {
        if (_navigationPreference != SlateNavigationEdge.Automatic)
            return _navigationPreference;

        return width > 0 && height > width
            ? SlateNavigationEdge.Top
            : SlateNavigationEdge.Left;
    }

    private Control BuildRoot(ContentControl body)
    {
        var railIsHorizontal =
            _activeEdge is SlateNavigationEdge.Top or SlateNavigationEdge.Bottom;
        var rail = BuildCommandRail(railIsHorizontal);
        var center = new Grid();

        if (railIsHorizontal)
        {
            center.RowDefinitions = _activeEdge == SlateNavigationEdge.Top
                ? new RowDefinitions("Auto,*")
                : new RowDefinitions("*,Auto");
            if (_activeEdge == SlateNavigationEdge.Top)
            {
                center.Children.Add(rail);
                center.Children.Add(body);
                Grid.SetRow(body, 1);
            }
            else
            {
                center.Children.Add(body);
                center.Children.Add(rail);
                Grid.SetRow(rail, 1);
            }
        }
        else
        {
            center.ColumnDefinitions = _activeEdge == SlateNavigationEdge.Left
                ? new ColumnDefinitions("Auto,*")
                : new ColumnDefinitions("*,Auto");
            if (_activeEdge == SlateNavigationEdge.Left)
            {
                center.Children.Add(rail);
                center.Children.Add(body);
                Grid.SetColumn(body, 1);
            }
            else
            {
                center.Children.Add(body);
                center.Children.Add(rail);
                Grid.SetColumn(rail, 1);
            }
        }

        var root = new Grid
        {
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Background),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                BuildIdentityBand(),
                center,
                BuildFooter(),
            },
        };
        Grid.SetRow(center, 1);
        Grid.SetRow(root.Children[2], 2);
        if (_quickActionsVisible)
        {
            var quickActions = BuildQuickActionsOverlay();
            root.Children.Add(quickActions);
            Grid.SetRow(quickActions, 1);
            quickActions.ZIndex = 10;
        }
        return root;
    }

    private Control BuildIdentityBand()
    {
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                CreateText("LOCAL · HOST", 10, SlateVisualTokens.Muted),
                CreateText(
                    DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                    12,
                    SlateVisualTokens.Text),
                CreateText("OBSERVATION", 10, SlateVisualTokens.Attention),
            },
        };
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        CreateText("NOVOLIS / SLATE", 15, SlateVisualTokens.CommandBright),
                        CreateText(_route, 28, SlateVisualTokens.Text, FontWeight.Light),
                    },
                },
                right,
            },
        };
        Grid.SetColumn(right, 1);
        return new Border
        {
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            Padding = new Thickness(18, 10, 18, 9),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = layout,
        };
    }

    private Control BuildCommandRail(bool horizontal)
    {
        var rail = new StackPanel
        {
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            Spacing = 5,
            Margin = new Thickness(horizontal ? 10 : 0, horizontal ? 8 : 12),
        };
        if (!horizontal)
            rail.Width = 53;

        // The primary rail mirrors the prototype: four stable destinations.
        // Capability actions live below the divider so the rail remains a
        // command surface instead of becoming a second application catalog.
        AddRailButton(rail, "HOME", "⌂", "home", "Workspace live tiles.");
        AddRailButton(rail, "APPS", "▦", "apps", "Launch and inspect applications.");
        AddRailButton(rail, "SYSTEM", "◈", "system", "Observed device and privacy state.");
        AddRailButton(rail, "TERMINAL", ">", "terminal", "Host-handled maintenance input.");

        rail.Children.Add(new Border
        {
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            Height = horizontal ? 1 : 8,
            Width = horizontal ? 32 : 1,
            Margin = new Thickness(4, 4),
        });
        AddRailButton(rail, "BROWSER", "◎", "browser", "Open a history-free private web session.");
        AddRailButton(rail, "FILES", "▤", "files", "Choose files and folders without recent-file history.");
        AddRailButton(rail, "BLUETOOTH", "⌁", "bluetooth", "Pair and discover nearby Bluetooth devices.");
        AddRailStatus(rail, "NETWORK", "UNVERIFIED", SlateVisualTokens.Attention, "Open system state.");
        AddRailStatus(
            rail,
            "CATALOG",
            _applications.Count == 0 ? "NOT CHECKED" : "OBSERVED",
            SlateVisualTokens.Information,
            "Open application catalog.");
        return new Border
        {
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = horizontal
                ? new Thickness(0, 0, 0, 1)
                : new Thickness(0, 0, 1, 0),
            Child = rail,
        };
    }

    private void AddRailButton(
        Panel rail,
        string label,
        string glyph,
        string route,
        string description)
    {
        var active = string.Equals(_route, route, StringComparison.Ordinal);
        var compact = _activeEdge is SlateNavigationEdge.Left or SlateNavigationEdge.Right;
        var width = compact ? 53 : 92;
        var content = compact
            ? (Control)CreateText(
                glyph,
                20,
                active ? SlateVisualTokens.Background : SlateVisualTokens.CommandBright)
            : new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    CreateText(
                        glyph,
                        20,
                        active ? SlateVisualTokens.Background : SlateVisualTokens.CommandBright),
                    CreateText(
                        label,
                        10,
                        active ? SlateVisualTokens.Background : SlateVisualTokens.Text,
                        FontWeight.Bold),
                },
            };
        var button = new Button
        {
            Width = width,
            MinWidth = width,
            MinHeight = 56,
            Padding = new Thickness(8, 6),
            Background = Brushes.Transparent,
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = content,
        };
        button.Click += (_, _) => NavigateTo(route);
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, $"{label}: {description}");
        rail.Children.Add(new Border
        {
            Background = SlateVisualTokens.Brush(
                active ? SlateVisualTokens.Command : SlateVisualTokens.Raised),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(0, 0, 0, active ? 0 : 1),
            CornerRadius = _activeEdge switch
            {
                SlateNavigationEdge.Top => new CornerRadius(0, 0, 18, 18),
                SlateNavigationEdge.Bottom => new CornerRadius(18, 18, 0, 0),
                SlateNavigationEdge.Right => new CornerRadius(18, 0, 0, 18),
                _ => new CornerRadius(0, 18, 18, 0),
            },
            Child = button,
        });
    }

    private void AddRailStatus(
        Panel rail,
        string label,
        string value,
        Color color,
        string description)
    {
        var compact = _activeEdge is SlateNavigationEdge.Left or SlateNavigationEdge.Right;
        var width = compact ? 53 : 110;
        var compactLabel = compact
            ? label switch
            {
                "NETWORK" => "NET",
                "CATALOG" => "CAT",
                _ => label,
            }
            : label;
        var compactValue = compact && value == "UNVERIFIED" ? "UNKNOWN" : value;
        var background = color == SlateVisualTokens.Attention
            ? SlateVisualTokens.AttentionDark
            : SlateVisualTokens.InformationDark;
        var button = new Button
        {
            Width = width,
            MinWidth = width,
            MinHeight = 48,
            Padding = new Thickness(8, 5),
            Background = Brushes.Transparent,
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    CreateText(compactLabel, 9, color, FontWeight.Bold),
                    CreateText(compactValue, compact ? 8 : 10, SlateVisualTokens.Muted),
                },
            },
        };
        button.Click += (_, _) => NavigateTo(label == "CATALOG" ? "apps" : "system");
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, $"{label}: {value}. {description}");
        rail.Children.Add(new Border
        {
            Background = SlateVisualTokens.Brush(background),
            BorderBrush = SlateVisualTokens.Brush(color),
            BorderThickness = _activeEdge is SlateNavigationEdge.Top or SlateNavigationEdge.Bottom
                ? new Thickness(0, 0, 0, 4)
                : new Thickness(4, 0, 0, 0),
            CornerRadius = _activeEdge switch
            {
                SlateNavigationEdge.Top => new CornerRadius(0, 0, 18, 18),
                SlateNavigationEdge.Bottom => new CornerRadius(18, 18, 0, 0),
                SlateNavigationEdge.Right => new CornerRadius(18, 0, 0, 18),
                _ => new CornerRadius(0, 18, 18, 0),
            },
            Child = button,
        });
    }

    private Control BuildFooter()
    {
        var refreshButton = CreateActionButton(
            "REFRESH CATALOG",
            () => _ = RefreshApplicationsAsync());
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            Children =
            {
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        CreateText(
                            "COMMAND / EXPLORE",
                            10,
                            SlateVisualTokens.CommandBright,
                            FontWeight.Bold),
                        CreateText(_statusMessage, 13, SlateVisualTokens.Muted),
                    },
                },
                refreshButton,
            },
        };
        Grid.SetColumn(refreshButton, 1);
        refreshButton.VerticalAlignment = VerticalAlignment.Center;
        return new Border
        {
            Padding = new Thickness(10, 7),
            Margin = new Thickness(10, 0),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = footer,
        };
    }

    private void NavigateTo(string route)
    {
        _route = route;
        _quickActionsVisible = false;
        RebuildLayout();
    }

    /// <summary>Shows the quick command surface when Android returns to the shell HOME.</summary>
    public void ShowQuickActions()
    {
        _quickActionsVisible = true;
        RebuildLayout();
    }

    /// <summary>Refreshes activity-bound capability state after a system surface returns.</summary>
    public void RefreshPlatformState() => _ = RefreshCapabilitiesAsync();

    private Control BuildQuickActionsOverlay()
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateText("QUICK ACTIONS", 11, SlateVisualTokens.CommandBright, FontWeight.Bold),
                CreateText(
                    $"SESSION · {_route.ToUpperInvariant()}",
                    12,
                    SlateVisualTokens.Text),
                CreateText(
                    $"CATALOG · {_launchableApplications.Count} OBSERVED",
                    12,
                    SlateVisualTokens.Muted),
                CreateText(
                    $"BROWSER · {_services.Browser.Status.Summary}",
                    12,
                    SlateVisualTokens.Information),
                CreateText(
                    $"FILES · {_services.Files.Status.Summary}",
                    12,
                    SlateVisualTokens.Files),
                CreateText(
                    $"BLUETOOTH · {_bluetoothSnapshot.Summary}",
                    12,
                    SlateVisualTokens.Information),
                CreateQuickAction("OPEN APPLICATIONS", () => NavigateTo("apps")),
                CreateQuickAction("PRIVATE BROWSER", () => NavigateTo("browser")),
                CreateQuickAction("FILES", () => NavigateTo("files")),
                CreateQuickAction("BLUETOOTH", () => NavigateTo("bluetooth")),
                CreateQuickAction("SYSTEM STATUS", () => NavigateTo("system")),
                CreateQuickAction("MAINTENANCE TERMINAL", () => NavigateTo("terminal")),
                CreateQuickAction("REFRESH CATALOG", () =>
                {
                    _quickActionsVisible = false;
                    RebuildLayout();
                    _ = RefreshApplicationsAsync();
                }),
                CreateQuickAction("DISMISS", () =>
                {
                    _quickActionsVisible = false;
                    RebuildLayout();
                }),
            },
        };
        return new Border
        {
            Width = Math.Min(360, Math.Max(280, Bounds.Width - 48)),
            MaxHeight = 560,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18),
            Padding = new Thickness(16),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Raised),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Command),
            BorderThickness = new Thickness(1, 1, 4, 1),
            CornerRadius = new CornerRadius(4, 18, 4, 18),
            Child = panel,
        };
    }

    private Button CreateQuickAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 48,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = SlateVisualTokens.Brush(SlateVisualTokens.InformationDark),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            Padding = new Thickness(12, 8),
        };
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, label);
        return button;
    }

    private Control BuildRouteBody() => _route switch
    {
        "apps" => BuildApplicationsBody(),
        "browser" => BuildBrowserBody(),
        "files" => BuildFilesBody(),
        "bluetooth" => BuildBluetoothBody(),
        "system" => BuildSystemBody(),
        "terminal" => BuildTerminalBody(),
        _ => BuildHomeBody(),
    };

    private Control BuildHomeBody()
    {
        var novolisCount = _launchableApplications.Count(application => application.IsNovolisApplication);
        var updateCount = _launchableApplications.Count(
            application => application.UpdateState == MobileUpdateState.Available);
        var updateText = _launchableApplications.Any(application => application.HasRecentUpdateCheck)
            ? $"{updateCount} updates available"
            : "Update check pending";

        var content = new StackPanel
        {
            Margin = new Thickness(24, 18),
            Spacing = 12,
            Children =
            {
                CreateText(
                    "good evening",
                    Bounds.Width >= 1100 ? 44 : 36,
                    SlateVisualTokens.Text,
                    FontWeight.Light),
                CreateText(
                    "WORKSPACE / 01 · YOUR DEVICE. YOUR WORKSPACE. YOUR CONTROL.",
                    11,
                    SlateVisualTokens.Muted,
                    FontWeight.Bold),
                BuildLiveTileBoard(
                    BuildLiveTile(
                        "terminal",
                        "HOST-HANDLED",
                        "Last session is local; execution boundary is visible.",
                        SlateVisualTokens.Text,
                        SlateVisualTokens.Command,
                        "terminal",
                        true),
                    BuildLiveTile(
                        "apps",
                        $"{novolisCount} Novolis · {_launchableApplications.Count} launchable",
                        updateText,
                        SlateVisualTokens.Text,
                        SlateVisualTokens.Information,
                        "apps",
                        true),
                    BuildLiveTile(
                        "privacy",
                        "UNVERIFIED",
                        "Isolation proof is not available to this surface.",
                        SlateVisualTokens.Attention,
                        SlateVisualTokens.Privacy,
                        "system",
                        true),
                    BuildLiveTile(
                        "browser",
                        _services.Browser.Status.Summary,
                        _services.Browser.Status.Detail,
                        SlateVisualTokens.Information,
                        SlateVisualTokens.InformationDark,
                        "browser",
                        true),
                    BuildLiveTile(
                        "files",
                        _services.Files.Status.Summary,
                        _services.Files.Status.Detail,
                        SlateVisualTokens.Text,
                        SlateVisualTokens.Files,
                        "files",
                        true),
                    BuildLiveTile(
                        "bluetooth",
                        _bluetoothSnapshot.Summary,
                        _bluetoothSnapshot.Detail,
                        SlateVisualTokens.Information,
                        SlateVisualTokens.InformationDark,
                        "bluetooth",
                        true),
                    BuildLiveTile(
                        "updates",
                        updateText.ToUpperInvariant(),
                        "Release catalog observation is host-controlled.",
                        SlateVisualTokens.Attention,
                        SlateVisualTokens.Updates,
                        "apps",
                        true),
                    BuildLiveTile(
                        "system",
                        "OBSERVATION PARTIAL",
                        "Battery · memory · storage are not queried here.",
                        SlateVisualTokens.Text,
                        SlateVisualTokens.System,
                        "system",
                        true),
                    BuildLiveTile(
                        "experiments",
                        "NO ACTIVE SESSION",
                        "No active experiment was reported.",
                        SlateVisualTokens.Text,
                        SlateVisualTokens.Experiments,
                        string.Empty,
                        false)),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        CreateText(
                            "COMMAND",
                            10,
                            SlateVisualTokens.Muted,
                            FontWeight.Bold),
                        CreateSoftButton("MANAGE APPS ↗", () => NavigateTo("apps")),
                        CreateSoftButton("SYSTEM STATUS ↗", () => NavigateTo("system")),
                    },
                },
                CreateReadOnlyNote(
                    "Slate is the primary surface. Android remains the compatibility layer beneath it."),
            },
        };
        return CreateScrollViewer(content);
    }

    private static Control BuildLiveTile(
        string title,
        string value,
        string detail,
        Color accent,
        Color background,
        string route,
        bool isInteractive)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                CreateText(title, 24, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(value, 16, accent, FontWeight.Bold),
                CreateText(detail, 12, SlateVisualTokens.Text),
            },
        };
        Grid.SetRow(grid.Children[1], 1);
        Grid.SetRow(grid.Children[2], 2);

        if (!isInteractive)
        {
            return new Border
            {
                MinHeight = 132,
                Padding = new Thickness(14),
                Background = SlateVisualTokens.Brush(background),
                CornerRadius = new CornerRadius(3),
                Child = grid,
            };
        }

        var tile = new Button
        {
            MinHeight = 132,
            Padding = new Thickness(14),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Background = SlateVisualTokens.Brush(background),
            BorderThickness = new Thickness(0),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            IsEnabled = isInteractive,
            Content = grid,
            Tag = route,
        };
        return tile;
    }

    private Control BuildLiveTileBoard(params Control[] tiles)
    {
        var columns = Bounds.Width >= 900 ? 4 : 2;
        var board = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                string.Join(",", Enumerable.Repeat("*", columns))),
            ColumnSpacing = 7,
            RowSpacing = 7,
        };
        var row = 0;
        var column = 0;
        var rowCount = 1;
        for (var index = 0; index < tiles.Length; index++)
        {
            var tile = tiles[index];
            var span = index == 0 || index >= tiles.Length - 2
                ? Math.Min(2, columns)
                : 1;
            if (column + span > columns)
            {
                row++;
                column = 0;
            }

            Grid.SetColumn(tile, column);
            Grid.SetRow(tile, row);
            Grid.SetColumnSpan(tile, span);
            WireTileRoute(tile);
            board.Children.Add(tile);
            rowCount = Math.Max(rowCount, row + 1);
            column += span;
            if (column >= columns)
            {
                row++;
                column = 0;
            }
        }

        for (var index = 0; index < rowCount; index++)
        {
            board.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        foreach (var tile in board.Children)
        {
            tile.HorizontalAlignment = HorizontalAlignment.Stretch;
            tile.VerticalAlignment = VerticalAlignment.Stretch;
        }

        return board;
    }

    private void WireTileRoute(Control tile)
    {
        if (tile is Button button &&
            button.Tag is string route &&
            !string.IsNullOrEmpty(route))
        {
            button.Click += (_, _) => NavigateTo(route);
        }
    }

    private Control BuildApplicationsBody()
    {
        var novolis = _launchableApplications
            .Where(application => application.IsNovolisApplication)
            .ToArray();
        var other = _launchableApplications
            .Where(application => !application.IsNovolisApplication)
            .ToArray();
        var catalog = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateText(
                    "applications",
                    Bounds.Width >= 1100 ? 40 : 32,
                    SlateVisualTokens.Text,
                    FontWeight.Light),
                CreateText(
                    "APPLICATIONS / 02 · NOVOLIS FIRST · DISPLAY NAME ASCENDING",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                BuildApplicationGroup("NOVOLIS", novolis, "Novolis applications are shown first."),
                BuildApplicationGroup("OTHER APPS", other, "Other Android applications follow below."),
            },
        };
        var inspector = BuildApplicationInspector();
        if (Bounds.Width >= 920)
        {
            var wide = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,320"),
                ColumnSpacing = 20,
                Children = { CreateScrollViewer(catalog), inspector },
            };
            Grid.SetColumn(inspector, 1);
            return wide;
        }

        catalog.Children.Add(inspector);
        return CreateScrollViewer(catalog);
    }

    private Control BuildBrowserBody()
    {
        var status = _services.Browser.Status;
        var address = new TextBox
        {
            MinHeight = 52,
            PlaceholderText = "Enter a host or HTTPS address, such as example.com",
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        var open = CreateActionButton(
            "OPEN PRIVATE SESSION",
            () => _ = OpenBrowserAsync(address));
        open.IsEnabled = status.IsAvailable;
        var clear = CreateActionButton(
            "CLEAR SESSION",
            () => _ = ClearBrowserSessionAsync());
        clear.IsEnabled = status.IsAvailable;

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { open, clear },
        };
        var policy = new Border
        {
            Padding = new Thickness(16),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.InformationDark),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Information),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 7,
                Children =
                {
                    CreateText(status.Summary, 13, SlateVisualTokens.Information, FontWeight.Bold),
                    CreateText(status.Mode, 20, SlateVisualTokens.Text, FontWeight.Light),
                    CreateText(status.Detail, 13, SlateVisualTokens.Muted),
                    CreateText(
                        status.StoresHistory
                            ? "History retention is enabled by the platform."
                            : "The shell keeps no URL history between sessions.",
                        12,
                        status.StoresHistory
                            ? SlateVisualTokens.Attention
                            : SlateVisualTokens.Success),
                },
            },
        };

        return CreateScrollViewer(new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 18,
            Children =
            {
                CreateText("browser", 34, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(
                    "PRIVATE WEB SESSION · NO SHELL HISTORY",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                policy,
                address,
                actionRow,
                CreateReadOnlyNote(
                    "Hosts are upgraded to HTTPS. Android WebView remains the compatibility layer; sign-in cookies and storage are erased when the session closes, while downloads and URL history stay disabled."),
            },
        });
    }

    private async Task OpenBrowserAsync(TextBox address)
    {
        var result = await _services.Browser.OpenAsync(address.Text ?? string.Empty)
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        RebuildLayout();
    }

    private async Task ClearBrowserSessionAsync()
    {
        var result = await _services.Browser.ClearSessionAsync()
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        RebuildLayout();
    }

    private Control BuildFilesBody()
    {
        var status = _services.Files.Status;
        var openFile = CreateActionButton(
            "OPEN ONE FILE",
            () => _ = OpenFilePickerAsync());
        var openFolder = CreateActionButton(
            "CHOOSE A FOLDER",
            () => _ = OpenFolderPickerAsync());
        openFile.IsEnabled = status.IsAvailable;
        openFolder.IsEnabled = status.IsAvailable;

        return CreateScrollViewer(new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 18,
            Children =
            {
                CreateText("files", 34, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(
                    "USER-GRANTED ACCESS · NO RECENT FILES",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                new Border
                {
                    Padding = new Thickness(16),
                    Background = SlateVisualTokens.Brush(SlateVisualTokens.Raised),
                    BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Files),
                    BorderThickness = new Thickness(1),
                    Child = new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            CreateText(status.Summary, 13, SlateVisualTokens.Files, FontWeight.Bold),
                            CreateText(status.AccessMode, 20, SlateVisualTokens.Text, FontWeight.Light),
                            CreateText(status.Detail, 13, SlateVisualTokens.Muted),
                            CreateText(
                                status.KeepsRecentHistory
                                    ? "Recent-file history is enabled by the platform."
                                    : "No recent file, folder, or path list is retained.",
                                12,
                                status.KeepsRecentHistory
                                    ? SlateVisualTokens.Attention
                                    : SlateVisualTokens.Success),
                        },
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { openFile, openFolder },
                },
                CreateReadOnlyNote(
                    "Android does not grant a normal application silent unrestricted storage access. The system picker is the strongest supported boundary: you choose exactly what the shell can read, and the shell does not persist that choice."),
            },
        });
    }

    private async Task OpenFilePickerAsync()
    {
        var result = await _services.Files.OpenFilePickerAsync()
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        RebuildLayout();
    }

    private async Task OpenFolderPickerAsync()
    {
        var result = await _services.Files.OpenFolderPickerAsync()
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        RebuildLayout();
    }

    private Control BuildBluetoothBody()
    {
        var snapshot = _bluetoothSnapshot;
        var discovery = CreateActionButton(
            snapshot.Availability == BluetoothAvailability.PermissionRequired
                ? "ALLOW BLUETOOTH ACCESS"
                : snapshot.IsDiscovering
                    ? "STOP SCAN"
                    : "SCAN NEARBY",
            () => _ = RunBluetoothActionAsync(snapshot));
        var settings = CreateActionButton(
            "BLUETOOTH SETTINGS",
            () => _ = OpenBluetoothSettingsAsync());
        discovery.IsEnabled = snapshot.Availability != BluetoothAvailability.Unavailable;
        settings.IsEnabled = snapshot.Availability != BluetoothAvailability.Unavailable;

        var devices = new StackPanel { Spacing = 8 };
        foreach (var device in snapshot.Devices
                     .OrderByDescending(device => device.IsPaired)
                     .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var deviceState = CreateText(
                device.IsPaired ? "PAIRED" : "DISCOVERED",
                10,
                device.IsPaired
                    ? SlateVisualTokens.Success
                    : SlateVisualTokens.Information,
                FontWeight.Bold);
            Grid.SetColumn(deviceState, 1);
            var deviceMeta = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    CreateText(
                        device.DisplayName,
                        18,
                        SlateVisualTokens.Text,
                        FontWeight.Light),
                    CreateText(
                        $"{device.Kind} · {device.Address}",
                        11,
                        SlateVisualTokens.Muted),
                },
            };
            var deviceGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                Children = { deviceMeta, deviceState },
            };
            if (!device.IsPaired)
            {
                var pair = new Button
                {
                    Content = "PAIR",
                    MinWidth = 78,
                    MinHeight = 42,
                    Padding = new Thickness(10, 6),
                    Background = SlateVisualTokens.Brush(SlateVisualTokens.InformationDark),
                    Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
                };
                pair.Click += (_, _) => _ = PairBluetoothAsync(device);
                AutomationProperties.SetName(pair, $"Pair {device.DisplayName}");
                Grid.SetColumn(pair, 2);
                deviceGrid.Children.Add(pair);
            }

            devices.Children.Add(new Border
            {
                Padding = new Thickness(14),
                Background = SlateVisualTokens.Brush(SlateVisualTokens.Raised),
                BorderBrush = SlateVisualTokens.Brush(
                    device.IsPaired
                        ? SlateVisualTokens.Success
                        : SlateVisualTokens.Border),
                BorderThickness = new Thickness(1),
                Child = deviceGrid,
            });
        }

        if (snapshot.Devices.Count == 0)
        {
            devices.Children.Add(CreateReadOnlyNote(
                snapshot.IsDiscovering
                    ? "Listening for nearby devices…"
                    : "No paired or discovered devices are currently visible."));
        }

        return CreateScrollViewer(new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 18,
            Children =
            {
                CreateText("bluetooth", 34, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(
                    "NEARBY DEVICES · EXPLICIT DISCOVERY",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                new Border
                {
                    Padding = new Thickness(16),
                    Background = SlateVisualTokens.Brush(SlateVisualTokens.InformationDark),
                    BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Information),
                    BorderThickness = new Thickness(1),
                    Child = new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            CreateText(snapshot.Summary, 13, SlateVisualTokens.Information, FontWeight.Bold),
                            CreateText(
                                snapshot.IsEnabled ? "ADAPTER ENABLED" : "ADAPTER OFF",
                                20,
                                SlateVisualTokens.Text,
                                FontWeight.Light),
                            CreateText(snapshot.Detail, 13, SlateVisualTokens.Muted),
                        },
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children = { discovery, settings },
                },
                CreateText(
                    $"DEVICES · {snapshot.Devices.Count}",
                    13,
                    SlateVisualTokens.Information,
                    FontWeight.Bold),
                devices,
                CreateReadOnlyNote(
                    "Discovery is foreground-only and user initiated. Pairing and device-specific permissions remain in Android's system flow."),
            },
        });
    }

    private async Task RunBluetoothActionAsync(BluetoothSnapshot snapshot)
    {
        var result = snapshot.Availability == BluetoothAvailability.PermissionRequired
            ? await _services.Bluetooth.RequestPermissionsAsync().ConfigureAwait(true)
            : snapshot.IsDiscovering
                ? await _services.Bluetooth.StopDiscoveryAsync().ConfigureAwait(true)
                : await _services.Bluetooth.StartDiscoveryAsync().ConfigureAwait(true);
        _statusMessage = result.Message;
        await RefreshCapabilitiesAsync().ConfigureAwait(true);
    }

    private async Task PairBluetoothAsync(BluetoothDeviceEntry device)
    {
        var result = await _services.Bluetooth
            .PairAsync(device.Address)
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        await RefreshCapabilitiesAsync().ConfigureAwait(true);
    }

    private async Task OpenBluetoothSettingsAsync()
    {
        var result = await _services.Bluetooth.OpenSettingsAsync()
            .ConfigureAwait(true);
        _statusMessage = result.Message;
        RebuildLayout();
    }

    private async Task RefreshCapabilitiesAsync()
    {
        if (_capabilityRefreshInProgress)
            return;

        _capabilityRefreshInProgress = true;
        try
        {
            _bluetoothSnapshot = await _services.Bluetooth
                .GetSnapshotAsync()
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _bluetoothSnapshot = BluetoothSnapshot.Unavailable(
                $"Bluetooth observation failed: {ex.Message}");
        }
        finally
        {
            _capabilityRefreshInProgress = false;
            RebuildLayout();
        }
    }

    private Control BuildApplicationGroup(
        string title,
        IReadOnlyList<MobileApplicationEntry> applications,
        string description)
    {
        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                CreateText($"{title} · {applications.Count}", 13, SlateVisualTokens.Information, FontWeight.Bold),
                CreateText(description, 13, SlateVisualTokens.Muted),
                BuildApplicationGrid(applications),
            },
        };
    }

    private Control BuildApplicationGrid(IReadOnlyList<MobileApplicationEntry> applications)
    {
        if (applications.Count == 0)
            return CreateReadOnlyNote("No launchable applications were observed.");

        var columns = Bounds.Width >= 1100 ? 3 : 2;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                string.Join(",", Enumerable.Repeat("*", columns))),
            ColumnSpacing = 7,
            RowSpacing = 7,
        };
        var ordered = applications
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var card = BuildApplicationCard(ordered[index]);
            Grid.SetColumn(card, index % columns);
            Grid.SetRow(card, index / columns);
            grid.Children.Add(card);
        }

        for (var row = 0; row < Math.Ceiling(ordered.Length / (double)columns); row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        return grid;
    }

    private Control BuildApplicationCard(MobileApplicationEntry application)
    {
        var update = application.UpdateState switch
        {
            MobileUpdateState.Available => $"UPDATE · {application.AvailableVersion ?? "available"}",
            MobileUpdateState.Current => "CURRENT",
            MobileUpdateState.Failed => "UPDATE FAILED",
            _ => application.HasRecentUpdateCheck
                ? "CHECKED · CURRENT UNKNOWN"
                : "CHECK NOT RUN",
        };
        var icon = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(5),
            Background = SlateVisualTokens.Brush(ApplicationAccent(application)),
            Child = CreateText("▣", 18, SlateVisualTokens.Text, FontWeight.Light),
        };
        var identity = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("44,*"),
            Children =
            {
                icon,
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        CreateText(application.DisplayName, 17, SlateVisualTokens.Text, FontWeight.Light),
                        CreateText(
                            application.IsNovolisApplication ? "NOVOLIS" : "ANDROID",
                            10,
                            SlateVisualTokens.Information,
                            FontWeight.Bold),
                    },
                },
            },
        };
        Grid.SetColumn(identity.Children[1], 1);
        var open = new Button
        {
            Padding = new Thickness(12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Raised),
            BorderThickness = new Thickness(0),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            Content = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    identity,
                    CreateText(
                        update,
                        11,
                        application.UpdateState == MobileUpdateState.Available
                            ? SlateVisualTokens.Attention
                            : SlateVisualTokens.Muted),
                    CreateText(
                        application.InstalledVersion ?? application.PackageName,
                        11,
                        SlateVisualTokens.Muted),
                },
            },
        };
        open.Click += (_, _) => _ = LaunchApplicationAsync(application);
        AutomationProperties.SetName(open, $"Open {application.DisplayName}");

        var details = new Button
        {
            Content = "DETAILS",
            Width = 88,
            MinHeight = 48,
            Padding = new Thickness(10, 6),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.InformationDark),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 8),
        };
        details.Click += (_, _) =>
        {
            _selectedApplication = application;
            RebuildLayout();
        };

        return new Border
        {
            MinHeight = 132,
            BorderBrush = SlateVisualTokens.Brush(
                application.UpdateState == MobileUpdateState.Available
                    ? SlateVisualTokens.Attention
                    : SlateVisualTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = new Grid
            {
                Children = { open, details },
            },
        };
    }

    private Control BuildApplicationInspector()
    {
        if (_selectedApplication is null)
        {
            return new Border
            {
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 12),
                Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
                BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        CreateText("INSPECTOR", 11, SlateVisualTokens.CommandBright, FontWeight.Bold),
                        CreateText("Select DETAILS on an application.", 18, SlateVisualTokens.Text, FontWeight.Light),
                        CreateText("The catalog stays visible while context is inspected.", 13, SlateVisualTokens.Muted),
                    },
                },
            };
        }

        var application = _selectedApplication;
        var openButton = new Button
        {
            Content = "OPEN APPLICATION",
            MinHeight = 48,
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Information),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
        };
        openButton.Click += (_, _) => _ = LaunchApplicationAsync(application);
        var updateButton = new Button
        {
            Content = application.UpdateState == MobileUpdateState.Available
                ? $"UPDATE NOW · {application.AvailableVersion}"
                : "CHECK FOR UPDATES",
            MinHeight = 48,
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Command),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Background),
        };
        updateButton.Click += (_, _) =>
        {
            _statusMessage = "Update workflow is host-controlled and has not been requested.";
        };

        return new Border
        {
            Padding = new Thickness(18),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Information),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    CreateText("INSPECTOR", 11, SlateVisualTokens.Information, FontWeight.Bold),
                    CreateText(application.DisplayName, 26, SlateVisualTokens.Text, FontWeight.Light),
                    CreateText(application.PackageName, 11, SlateVisualTokens.Muted),
                    CreateStateRow(
                        "INSTALLATION",
                        application.CanOpen ? "LAUNCHABLE" : "HOME OWNER",
                        SlateVisualTokens.Success),
                    CreateStateRow(
                        "UPDATE PROVENANCE",
                        application.HasRecentUpdateCheck ? "TRUSTED CHECK" : "UNKNOWN",
                        application.HasRecentUpdateCheck
                            ? SlateVisualTokens.Success
                            : SlateVisualTokens.Attention),
                    CreateText(
                        "Tap opens the application. Details exposes secondary actions.",
                        13,
                        SlateVisualTokens.Muted),
                    openButton,
                    updateButton,
                },
            },
        };
    }

    private Control BuildSystemBody()
    {
        var content = new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 18,
            Children =
            {
                CreateText("system", 34, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(
                    "OBSERVED STATE · PROVENANCE IS VISIBLE",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                BuildStateGrid(
                    CreateStateCard(
                        "NETWORK",
                        "UNVERIFIED",
                        "No enforcement proof is exposed by this surface.",
                        SlateVisualTokens.Attention,
                        SlateVisualTokens.AttentionDark),
                    CreateStateCard(
                        "ACCOUNTS",
                        "UNKNOWN",
                        "Account state was not queried.",
                        SlateVisualTokens.Attention,
                        SlateVisualTokens.AttentionDark),
                    CreateStateCard(
                        "HOME",
                        "OBSERVED",
                        "Novolis owns the current HOME activity.",
                        SlateVisualTokens.Success,
                        SlateVisualTokens.SuccessDark),
                    CreateStateCard(
                        "APPLICATIONS",
                        $"{_launchableApplications.Count} OBSERVED",
                        "Launchable entries sorted by display name.",
                        SlateVisualTokens.Information,
                        SlateVisualTokens.InformationDark),
                    CreateStateCard(
                        "BROWSER",
                        _services.Browser.Status.Summary,
                        _services.Browser.Status.Detail,
                        SlateVisualTokens.Information,
                        SlateVisualTokens.InformationDark),
                    CreateStateCard(
                        "FILES",
                        _services.Files.Status.Summary,
                        _services.Files.Status.Detail,
                        SlateVisualTokens.Files,
                        SlateVisualTokens.SuccessDark),
                    CreateStateCard(
                        "BLUETOOTH",
                        _bluetoothSnapshot.Summary,
                        _bluetoothSnapshot.Detail,
                        SlateVisualTokens.Information,
                        SlateVisualTokens.InformationDark)),
                CreateReadOnlyNote(
                    "Requested configuration, observed Android state, and externally verified enforcement are intentionally different states."),
            },
        };
        return CreateScrollViewer(content);
    }

    private Control BuildTerminalBody()
    {
        var output = CreateText(
            "nme> system info\n"
            + "PRODUCT  Novolis Mobile Experience\n"
            + "UX       Slate\n"
            + "MODE     Offline first\n"
            + $"APPS     {_launchableApplications.Count} launchable entries\n"
            + "BOUNDARY Host-handled input; execution is not implied."
            + _terminalTranscript,
            14,
            SlateVisualTokens.Text);
        var input = new TextBox
        {
            MinHeight = 48,
            PlaceholderText = "Enter a host-handled command",
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8),
        };
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                SubmitTerminalCommand(input, output);
                args.Handled = true;
            }
        };

        var send = CreateActionButton("SEND", () => SubmitTerminalCommand(input, output));
        var inputRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            Children = { input, send },
        };
        Grid.SetColumn(send, 1);
        return CreateScrollViewer(new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 18,
            Children =
            {
                CreateText("terminal", 34, SlateVisualTokens.Text, FontWeight.Light),
                CreateText(
                    "HOST-HANDLED INPUT · EXECUTION BOUNDARY VISIBLE",
                    11,
                    SlateVisualTokens.CommandBright,
                    FontWeight.Bold),
                new Border
                {
                    Padding = new Thickness(16),
                    Background = SlateVisualTokens.Brush(SlateVisualTokens.Background),
                    BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
                    BorderThickness = new Thickness(1),
                    Child = output,
                },
                inputRow,
            },
        });
    }

    private void SubmitTerminalCommand(TextBox input, TextBlock output)
    {
        var command = input.Text?.Trim();
        if (string.IsNullOrEmpty(command))
            return;

        _terminalTranscript +=
            $"\n\nnme> {command}\nHOST BOUNDARY · command recorded; no execution requested.";
        output.Text +=
            $"\n\nnme> {command}\nHOST BOUNDARY · command recorded; no execution requested.";
        input.Clear();
        _statusMessage = "Terminal command recorded without execution.";
    }

    private async Task RefreshApplicationsAsync()
    {
        if (_refreshInProgress)
            return;

        _refreshInProgress = true;
        _statusMessage = "Observing launchable applications…";
        try
        {
            _applications = await _catalog.DiscoverAsync().ConfigureAwait(true);
            _launchableApplications = _applications
                .Where(application => !application.IsHomeApplication)
                .OrderBy(application => application.IsNovolisApplication ? 0 : 1)
                .ThenBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _statusMessage = _launchableApplications.Count == 0
                ? "No launchable applications were observed."
                : $"{_launchableApplications.Count} launchable applications observed.";
        }
        catch (Exception ex)
        {
            _applications = [];
            _launchableApplications = [];
            _statusMessage = $"Observation failed; retry is available: {ex.Message}";
        }
        finally
        {
            _refreshInProgress = false;
            RebuildLayout();
        }
    }

    private async Task LaunchApplicationAsync(MobileApplicationEntry application)
    {
        if (!application.CanOpen)
        {
            _statusMessage = $"{application.DisplayName} owns HOME and cannot open itself.";
            return;
        }

        _statusMessage = $"Opening {application.DisplayName}…";
        try
        {
            var launched = await _catalog.LaunchAsync(application).ConfigureAwait(true);
            _statusMessage = launched
                ? $"Opened {application.DisplayName}."
                : $"Android declined the launch request for {application.DisplayName}.";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Could not open {application.DisplayName}: {ex.Message}";
        }
        finally
        {
            RebuildLayout();
        }
    }

    private static Color ApplicationAccent(MobileApplicationEntry application)
    {
        if (!application.IsNovolisApplication)
            return SlateVisualTokens.Disabled;

        var hash = 17u;
        foreach (var character in application.PackageName)
            hash = (hash * 31u) + character;

        return (hash % 5u) switch
        {
            0 => SlateVisualTokens.Information,
            1 => SlateVisualTokens.Privacy,
            2 => SlateVisualTokens.Files,
            3 => SlateVisualTokens.Updates,
            _ => SlateVisualTokens.Danger,
        };
    }

    private Control BuildStateGrid(params Control[] cards)
    {
        var columns = Bounds.Width >= 1100 ? 4 : 2;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                string.Join(",", Enumerable.Repeat("*", columns))),
            ColumnSpacing = 7,
            RowSpacing = 7,
        };
        for (var index = 0; index < cards.Length; index++)
        {
            var card = cards[index];
            Grid.SetColumn(card, index % columns);
            Grid.SetRow(card, index / columns);
            grid.Children.Add(card);
        }

        for (var row = 0; row < Math.Ceiling(cards.Length / (double)columns); row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        return grid;
    }

    private static Control CreateStateCard(
        string label,
        string value,
        string detail,
        Color accent,
        Color background) =>
        new Border
        {
            MinHeight = 126,
            Padding = new Thickness(14),
            Background = SlateVisualTokens.Brush(background),
            BorderBrush = SlateVisualTokens.Brush(accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    CreateText(label, 11, accent, FontWeight.Bold),
                    CreateText(value, 22, SlateVisualTokens.Text, FontWeight.Light),
                    CreateText(detail, 12, SlateVisualTokens.Muted),
                },
            },
        };

    private static Control CreateStateRow(string label, string value, Color accent) =>
        new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                CreateText(label, 10, SlateVisualTokens.Muted, FontWeight.Bold),
                CreateText(value, 10, accent, FontWeight.Bold),
            },
        };

    private static ScrollViewer CreateScrollViewer(Control content) =>
        new()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
        };

    private static Control CreateReadOnlyNote(string text) =>
        new Border
        {
            Padding = new Thickness(14),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Surface),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(1),
            Child = CreateText(text, 13, SlateVisualTokens.Muted),
        };

    private Button CreateSoftButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinHeight = 48,
            Padding = new Thickness(13, 8),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Raised),
            BorderBrush = SlateVisualTokens.Brush(SlateVisualTokens.Border),
            BorderThickness = new Thickness(1),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Text),
            FontSize = 12,
        };
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, label);
        return button;
    }

    private Button CreateActionButton(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 112,
            MinHeight = 48,
            Padding = new Thickness(13, 8),
            Background = SlateVisualTokens.Brush(SlateVisualTokens.Command),
            Foreground = SlateVisualTokens.Brush(SlateVisualTokens.Background),
            FontWeight = FontWeight.Bold,
        };
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, label);
        return button;
    }

    private static TextBlock CreateText(
        string text,
        double size,
        Color color,
        FontWeight? weight = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = SlateVisualTokens.Brush(color),
            TextWrapping = TextWrapping.Wrap,
        };
        if (weight is not null)
            block.FontWeight = weight.Value;
        return block;
    }
}
