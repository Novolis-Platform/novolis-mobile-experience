namespace Novolis.MobileExperience.Provisioning;

/// <summary>Result of a cleanup apply phase.</summary>
public sealed record AndroidCleanupExecution
{
    /// <summary>Whether every planned removal completed.</summary>
    public bool Ok { get; init; }

    /// <summary>APK backups created before package changes.</summary>
    public IReadOnlyList<string> Backups { get; init; } = [];

    /// <summary>Packages successfully removed from the selected Android user.</summary>
    public IReadOnlyList<string> RemovedPackages { get; init; } = [];

    /// <summary>Failure message, when the apply phase stopped.</summary>
    public string? Failure { get; init; }
}
