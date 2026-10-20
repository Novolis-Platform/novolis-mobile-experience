namespace Novolis.MobileExperience.Shell;

/// <summary>Bluetooth device information safe for display in the shell.</summary>
public sealed record BluetoothDeviceEntry
{
    /// <summary>Stable Android device address when the platform exposes it.</summary>
    public required string Address { get; init; }

    /// <summary>Friendly name, or a neutral fallback when it is unavailable.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether the device is paired with Android.</summary>
    public required bool IsPaired { get; init; }

    /// <summary>Whether the device was found during the current discovery pass.</summary>
    public required bool WasDiscovered { get; init; }

    /// <summary>Android-reported device category.</summary>
    public required string Kind { get; init; }
}
