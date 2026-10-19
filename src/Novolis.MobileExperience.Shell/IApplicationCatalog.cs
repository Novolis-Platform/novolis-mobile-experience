namespace Novolis.MobileExperience.Shell;

/// <summary>Discovers and launches Android applications visible to the shell.</summary>
public interface IApplicationCatalog
{
    /// <summary>Returns launchable applications for the current device.</summary>
    Task<IReadOnlyList<MobileApplicationEntry>> DiscoverAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Requests launch of one discovered application.</summary>
    Task<bool> LaunchAsync(
        MobileApplicationEntry application,
        CancellationToken cancellationToken = default);
}
