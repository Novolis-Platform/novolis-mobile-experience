using Android.Content;
using Android.Webkit;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

internal sealed class PrivateBrowserWebViewClient : WebViewClient
{
    private readonly Context _context;
    private readonly Action<string?> _addressChanged;
    private readonly Action<string?> _statusChanged;

    public PrivateBrowserWebViewClient(
        Context context,
        Action<string?> addressChanged,
        Action<string?> statusChanged)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _addressChanged = addressChanged ?? throw new ArgumentNullException(nameof(addressChanged));
        _statusChanged = statusChanged ?? throw new ArgumentNullException(nameof(statusChanged));
    }

    public override bool ShouldOverrideUrlLoading(
        WebView? view,
        IWebResourceRequest? request)
    {
        var url = request?.Url?.ToString();
        return HandleNavigation(view, url);
    }

    public override bool ShouldOverrideUrlLoading(WebView? view, string? url) =>
        HandleNavigation(view, url);

    public override void OnPageFinished(WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        _addressChanged(url);
        _statusChanged("PRIVATE SESSION · READY");
    }

    public override void OnReceivedError(
        WebView? view,
        IWebResourceRequest? request,
        WebResourceError? error)
    {
        base.OnReceivedError(view, request, error);
        if (request?.IsForMainFrame == true)
        {
            var detail = error?.Description?.ToString();
            _statusChanged(string.IsNullOrWhiteSpace(detail)
                ? "LOAD FAILED · NETWORK ERROR"
                : $"LOAD FAILED · {detail}");
        }
    }

    public override void OnReceivedHttpError(
        WebView? view,
        IWebResourceRequest? request,
        WebResourceResponse? errorResponse)
    {
        base.OnReceivedHttpError(view, request, errorResponse);
        if (request?.IsForMainFrame == true)
        {
            var code = errorResponse?.StatusCode ?? 0;
            _statusChanged($"LOAD FAILED · HTTP {code}");
        }
    }

    private bool HandleNavigation(WebView? view, string? url)
    {
        if (!global::System.Uri.TryCreate(
                url,
                global::System.UriKind.Absolute,
                out var address))
            return true;

        if (address.Scheme is "https")
            return false;

        if (address.Scheme is "http"
            && PrivacyBrowserAddress.TryNormalize(url, out var upgraded))
        {
            view?.LoadUrl(upgraded);
            return true;
        }

        var androidUri = global::Android.Net.Uri.Parse(address.AbsoluteUri);
        if (androidUri is null)
            return true;

        try
        {
            var intent = new Intent(Intent.ActionView);
            intent.SetData(androidUri);
            intent.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // Unsupported external schemes remain inside the privacy boundary.
        }

        return true;
    }
}
