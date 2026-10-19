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
}
