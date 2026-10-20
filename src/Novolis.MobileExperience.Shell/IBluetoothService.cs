namespace Novolis.MobileExperience.Shell;

/// <summary>Provides explicit, user-initiated Bluetooth management.</summary>
public interface IBluetoothService
{
    /// <summary>Reads the current adapter and device snapshot.</summary>
    Task<BluetoothSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Requests the minimum Android permissions needed for discovery.</summary>
    Task<MobileActionResult> RequestPermissionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Starts one foreground discovery pass.</summary>
    Task<MobileActionResult> StartDiscoveryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Stops the current discovery pass, when one is running.</summary>
    Task<MobileActionResult> StopDiscoveryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Starts Android's system pairing flow for one discovered device.</summary>
    Task<MobileActionResult> PairAsync(
        string address,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the Android Bluetooth settings surface.</summary>
    Task<MobileActionResult> OpenSettingsAsync(
        CancellationToken cancellationToken = default);
}
