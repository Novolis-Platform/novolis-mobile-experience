namespace Novolis.MobileExperience.Shell;

/// <summary>Safe no-op capability set used by non-Android hosts.</summary>
public sealed class EmptyMobileExperienceServices : IMobileExperienceServices
{
    /// <summary>Shared immutable no-op instance.</summary>
    public static EmptyMobileExperienceServices Instance { get; } = new();

    /// <inheritdoc />
    public IPrivacyBrowserService Browser { get; } = new EmptyPrivacyBrowserService();

    /// <inheritdoc />
    public IFileAccessService Files { get; } = new EmptyFileAccessService();

    /// <inheritdoc />
    public IBluetoothService Bluetooth { get; } = new EmptyBluetoothService();
}
