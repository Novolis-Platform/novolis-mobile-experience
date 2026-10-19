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
}
