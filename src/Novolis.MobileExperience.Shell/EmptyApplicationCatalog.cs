namespace Novolis.MobileExperience.Shell;

/// <summary>Safe fallback used when a host has no Android PackageManager.</summary>
public sealed class EmptyApplicationCatalog : IApplicationCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<MobileApplicationEntry>> DiscoverAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MobileApplicationEntry>>([]);

    /// <inheritdoc />
    public Task<bool> LaunchAsync(
        MobileApplicationEntry application,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
