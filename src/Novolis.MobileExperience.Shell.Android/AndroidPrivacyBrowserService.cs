using Android.App;
using Android.Content;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

/// <summary>Opens the shell-owned transient Android WebView browser.</summary>
public sealed class AndroidPrivacyBrowserService : IPrivacyBrowserService
{
    private readonly Activity _activity;

    /// <summary>Creates a browser service bound to the visible shell activity.</summary>
    public AndroidPrivacyBrowserService(Activity activity)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <inheritdoc />
    public PrivacyBrowserStatus Status { get; } = new()
    {
        IsAvailable = true,
        Mode = "Isolated Android WebView",
        Summary = "PRIVATE BROWSER READY",
        Detail = "Sign-in cookies and web storage exist only for this session and are erased on close; downloads and URL history stay disabled.",
        StoresHistory = false,
    };

    /// <inheritdoc />
    public Task<MobileActionResult> OpenAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PrivacyBrowserAddress.TryNormalize(address, out var normalized))
            return Task.FromResult(MobileActionResult.Failure(
                "Enter a host or HTTP(S) address, such as example.com."));

        var intent = new Intent(_activity, typeof(PrivacyBrowserActivity));
        intent.PutExtra(PrivacyBrowserActivity.StartAddressExtra, normalized);
        try
        {
            _activity.StartActivity(intent);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MobileActionResult.Failure(
                $"Android could not open the private browser: {ex.Message}"));
        }

        return Task.FromResult(MobileActionResult.Success(
            "Private browser session opened."));
    }

    /// <inheritdoc />
    public Task<MobileActionResult> ClearSessionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrivacyBrowserActivity.ClearSessionData();
        return Task.FromResult(MobileActionResult.Success(
            "Transient browser data cleared."));
    }
}
