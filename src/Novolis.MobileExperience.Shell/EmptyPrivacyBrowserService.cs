namespace Novolis.MobileExperience.Shell;

internal sealed class EmptyPrivacyBrowserService : IPrivacyBrowserService
{
    public PrivacyBrowserStatus Status { get; } = new()
    {
        IsAvailable = false,
        Mode = "Platform not connected",
        Summary = "BROWSER NOT AVAILABLE",
        Detail = "This host does not provide a browser surface.",
        StoresHistory = false,
    };

    public Task<MobileActionResult> OpenAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "No browser surface is connected on this host."));
    }

    public Task<MobileActionResult> ClearSessionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Success(
            "There is no browser session to clear."));
    }
}
