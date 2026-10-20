using Android.Webkit;
using Java.Interop;

namespace Novolis.MobileExperience.Shell.Android;

internal sealed class PrivateBrowserDownloadListener : Java.Lang.Object, IDownloadListener
{
    private readonly Action<string?, string?, string?, string?, long> _onDownload;

    public PrivateBrowserDownloadListener(
        Action<string?, string?, string?, string?, long> onDownload)
    {
        _onDownload = onDownload ?? throw new ArgumentNullException(nameof(onDownload));
    }

    public void OnDownloadStart(
        string? url,
        string? userAgent,
        string? contentDisposition,
        string? mimetype,
        long contentLength) =>
        _onDownload(url, userAgent, contentDisposition, mimetype, contentLength);
}
