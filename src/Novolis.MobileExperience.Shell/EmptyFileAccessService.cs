namespace Novolis.MobileExperience.Shell;

internal sealed class EmptyFileAccessService : IFileAccessService
{
    public FileAccessStatus Status { get; } = new()
    {
        IsAvailable = false,
        AccessMode = "Platform not connected",
        KeepsRecentHistory = false,
        Summary = "FILES NOT AVAILABLE",
        Detail = "This host does not provide a file picker.",
    };

    public Task<MobileActionResult> OpenFilePickerAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "No file picker is connected on this host."));
    }

    public Task<MobileActionResult> OpenFolderPickerAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "No folder picker is connected on this host."));
    }
}
