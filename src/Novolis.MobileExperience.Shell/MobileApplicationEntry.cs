namespace Novolis.MobileExperience.Shell;

/// <summary>One launchable application discovered from Android PackageManager.</summary>
public sealed record MobileApplicationEntry
{
    /// <summary>Android application id.</summary>
    public required string PackageName { get; init; }

    /// <summary>User-facing application label.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Fully qualified launch activity name.</summary>
    public required string ActivityName { get; init; }

    /// <summary>Whether this entry is the shell that currently owns HOME.</summary>
    public bool IsHomeApplication { get; init; }

    /// <summary>Whether the shell should render an open action for this entry.</summary>
    public bool CanOpen => !IsHomeApplication;

    /// <summary>Update state from the most recent trusted catalog check.</summary>
    public MobileUpdateState UpdateState { get; init; } = MobileUpdateState.Unknown;

    /// <summary>Installed version reported by Android, when available.</summary>
    public string? InstalledVersion { get; init; }

    /// <summary>Trusted version available from the release catalog, when known.</summary>
    public string? AvailableVersion { get; init; }

    /// <summary>Whether the shell has a current update check for this entry.</summary>
    public bool HasRecentUpdateCheck { get; init; }

    /// <summary>Whether this entry belongs to the Novolis application family.</summary>
    public bool IsNovolisApplication =>
        PackageName.StartsWith("com.novolis.", StringComparison.OrdinalIgnoreCase)
        || PackageName.StartsWith("dev.novolis.", StringComparison.OrdinalIgnoreCase);
}
