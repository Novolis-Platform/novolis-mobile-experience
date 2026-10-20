using Android.App;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

/// <summary>Android capability composition root for the Novolis shell.</summary>
public sealed class AndroidMobileExperienceServices : IMobileExperienceServices, IDisposable
{
    /// <summary>Creates platform capabilities for the visible shell activity.</summary>
    public AndroidMobileExperienceServices(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        Browser = new AndroidPrivacyBrowserService(activity);
        Files = new AndroidFileAccessService(activity);
        Bluetooth = new AndroidBluetoothService(activity);
    }

    /// <inheritdoc />
    public IPrivacyBrowserService Browser { get; }

    /// <inheritdoc />
    public IFileAccessService Files { get; }

    /// <inheritdoc />
    public IBluetoothService Bluetooth { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Bluetooth is IDisposable disposable)
            disposable.Dispose();
    }
}
