namespace Novolis.MobileExperience.Shell;

/// <summary>Platform capabilities composed into the Novolis tablet shell.</summary>
public interface IMobileExperienceServices
{
    /// <summary>Privacy browser wrapper.</summary>
    IPrivacyBrowserService Browser { get; }

    /// <summary>User-mediated file access surface.</summary>
    IFileAccessService Files { get; }

    /// <summary>Explicit Bluetooth management surface.</summary>
    IBluetoothService Bluetooth { get; }
}
