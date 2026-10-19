using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Novolis.Avalonia.GraphicalProfile;

namespace Novolis.MobileExperience.Shell;

/// <summary>Initial tablet-oriented shell surface and application launcher.</summary>
public sealed class ShellView : UserControl
{
    private readonly IApplicationCatalog _catalog;
    private readonly StackPanel _applicationList = new();
    private readonly TextBlock _status = new();
    private readonly Button _refreshButton;
    private bool _hasLoaded;

    /// <summary>Creates a shell backed by a platform application catalog.</summary>
    public ShellView(IApplicationCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _refreshButton = CreateButton(
            "Refresh applications",
            "primary-button",
            "Refresh the installed Android application list");
        _refreshButton.Click += (_, _) => _ = RefreshApplicationsAsync();

        _status.Text = "Ready to inspect installed applications.";
        _status.Classes.Add("body-copy");
        GraphicalProfileBinding.Bind(
            _status,
            TextBlock.ForegroundProperty,
            GraphicalProfile.MutedResourceKey);

        Content = BuildContent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        _ = RefreshApplicationsAsync();
    }

    private Control BuildContent()
    {
        var root = new Border
        {
            Padding = new Thickness(24),
            Child = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 18,
                    Children =
                    {
                        BuildHeader(),
                        BuildStatusCard(),
                        new TextBlock
                        {
                            Text = "Installed applications",
                            Classes = { "page-title" },
                        },
                        _applicationList,
                    },
                },
            },
        };
        GraphicalProfileBinding.Bind(
            root,
            Border.BackgroundProperty,
            GraphicalProfile.BackgroundResourceKey);
        return root;
    }

    private Control BuildHeader()
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 18,
            RowSpacing = 6,
        };

        var eyebrow = new TextBlock
        {
            Text = "NOVOLIS MOBILE EXPERIENCE",
            Classes = { "eyebrow" },
        };
        Grid.SetRow(eyebrow, 0);
        Grid.SetColumn(eyebrow, 0);
        header.Children.Add(eyebrow);

        var title = new TextBlock
        {
            Text = "A calm home for approved Android tools",
            Classes = { "page-title" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
        };
        Grid.SetRow(title, 1);
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        _refreshButton.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRowSpan(_refreshButton, 3);
        Grid.SetColumn(_refreshButton, 1);
        header.Children.Add(_refreshButton);

        var subtitle = new TextBlock
        {
            Text = "The first slice reads PackageManager and launches existing applications. Network and installation policy stay explicit.",
            Classes = { "body-copy" },
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
        };
        Grid.SetRow(subtitle, 2);
        Grid.SetColumn(subtitle, 0);
        header.Children.Add(subtitle);

        return header;
    }

    private Control BuildStatusCard()
    {
        var card = new Border
        {
            Classes = { "ngp-card" },
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock
                    {
                        Text = "SESSION STATUS",
                        Classes = { "eyebrow" },
                    },
                    _status,
                },
            },
        };
        return card;
    }

    private async Task RefreshApplicationsAsync()
    {
        if (!_refreshButton.IsEnabled)
            return;

        _refreshButton.IsEnabled = false;
        _status.Text = "Reading launchable applications…";
        try
        {
            var applications = await _catalog.DiscoverAsync().ConfigureAwait(true);
            _applicationList.Children.Clear();
            if (applications.Count == 0)
            {
                _applicationList.Children.Add(
                    CreateMessageCard(
                        "No launchable applications were reported by Android PackageManager."));
                _status.Text = "PackageManager returned no launcher activities.";
                return;
            }

            foreach (var application in applications)
                _applicationList.Children.Add(CreateApplicationCard(application));

            _status.Text =
                $"PackageManager reported {applications.Count} launchable application activities.";
        }
        catch (Exception ex)
        {
            _applicationList.Children.Clear();
            _applicationList.Children.Add(
                CreateMessageCard($"Application discovery failed: {ex.Message}"));
            _status.Text = "The shell remains available; discovery can be retried.";
        }
        finally
        {
            _refreshButton.IsEnabled = true;
        }
    }

    private Control CreateApplicationCard(MobileApplicationEntry application)
    {
        var launchButton = CreateButton(
            "Open",
            "action-button",
            $"Open {application.DisplayName}");
        launchButton.Click += async (_, _) =>
        {
            launchButton.IsEnabled = false;
            try
            {
                var launched = await _catalog.LaunchAsync(application)
                    .ConfigureAwait(true);
                _status.Text = launched
                    ? $"Opened {application.DisplayName}."
                    : $"Android declined the launch request for {application.DisplayName}.";
            }
            catch (Exception ex)
            {
                _status.Text = $"Could not open {application.DisplayName}: {ex.Message}";
            }
            finally
            {
                launchButton.IsEnabled = true;
            }
        };

        var details = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = application.DisplayName,
                    Classes = { "body-copy" },
                    FontWeight = global::Avalonia.Media.FontWeight.Bold,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                },
                new TextBlock
                {
                    Text = application.PackageName,
                    Classes = { "body-copy" },
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                },
            },
        };

        var row = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                launchButton,
                details,
            },
        };
        DockPanel.SetDock(launchButton, Dock.Right);

        return new Border
        {
            Classes = { "ngp-card" },
            Child = row,
        };
    }

    private static Control CreateMessageCard(string message) =>
        new Border
        {
            Classes = { "ngp-card" },
            Child = new TextBlock
            {
                Text = message,
                Classes = { "body-copy" },
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            },
        };

    private static Button CreateButton(
        string label,
        string className,
        string description)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 140,
        };
        button.Classes.Add(className);
        ToolTip.SetTip(button, description);
        AutomationProperties.SetName(button, description);
        return button;
    }
}
