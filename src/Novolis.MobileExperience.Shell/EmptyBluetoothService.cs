namespace Novolis.MobileExperience.Shell;

internal sealed class EmptyBluetoothService : IBluetoothService
{
    public Task<BluetoothSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BluetoothSnapshot.Unavailable(
            "This host does not provide Android Bluetooth controls."));
    }

    public Task<MobileActionResult> RequestPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "Bluetooth permissions are not available on this host."));
    }

    public Task<MobileActionResult> StartDiscoveryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "Bluetooth discovery is not available on this host."));
    }

    public Task<MobileActionResult> StopDiscoveryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Success(
            "No Bluetooth discovery pass is running."));
    }

    public Task<MobileActionResult> PairAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "Bluetooth pairing is not available on this host."));
    }

    public Task<MobileActionResult> OpenSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(MobileActionResult.Failure(
            "Bluetooth settings are not available on this host."));
    }
}
