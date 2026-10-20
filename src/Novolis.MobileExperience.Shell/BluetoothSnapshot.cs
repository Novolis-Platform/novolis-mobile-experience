namespace Novolis.MobileExperience.Shell;

/// <summary>Point-in-time Bluetooth state for the shell.</summary>
public sealed record BluetoothSnapshot
{
    /// <summary>Whether the adapter exists and is usable.</summary>
    public required BluetoothAvailability Availability { get; init; }

    /// <summary>Whether the Android adapter is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether a discovery pass is currently running.</summary>
    public required bool IsDiscovering { get; init; }

    /// <summary>Current bonded and discovered devices.</summary>
    public required IReadOnlyList<BluetoothDeviceEntry> Devices { get; init; }

    /// <summary>Short explanation of the current state.</summary>
    public required string Summary { get; init; }

    /// <summary>Permission and pairing guidance for the user.</summary>
    public required string Detail { get; init; }

    /// <summary>Creates an unavailable empty snapshot.</summary>
    public static BluetoothSnapshot Unavailable(string detail) =>
        new()
        {
            Availability = BluetoothAvailability.Unavailable,
            IsEnabled = false,
            IsDiscovering = false,
            Devices = [],
            Summary = "BLUETOOTH UNAVAILABLE",
            Detail = detail,
        };
}
