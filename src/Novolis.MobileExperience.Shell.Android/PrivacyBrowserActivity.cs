using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

[Activity(
    Label = "Novolis Private Browser",
    Exported = true,
    Theme = "@style/MainTheme",
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "http", "https" })]
public sealed class PrivacyBrowserActivity : Activity
{
    /// <summary>Intent extra containing the first web address.</summary>
    public const string StartAddressExtra = "novolis.private-browser.address";

    private EditText? _address;
    private TextView? _status;
    private WebView? _webView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ClearSessionData();

        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        root.SetBackgroundColor(global::Android.Graphics.Color.Rgb(14, 17, 22));

        var toolbar = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
        };
        toolbar.SetPadding(10, 10, 10, 6);

        var close = CreateButton("CLOSE");
        close.Click += (_, _) => Finish();
        toolbar.AddView(close, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent));

        var initialAddress = Intent?.GetStringExtra(StartAddressExtra)
            ?? Intent?.DataString
            ?? string.Empty;
        _address = new EditText(this)
        {
            Text = initialAddress,
            Hint = "https://example.com",
        };
        _address.SetTextColor(global::Android.Graphics.Color.White);
        _address.SetHintTextColor(global::Android.Graphics.Color.Rgb(141, 160, 174));
        _address.SetSingleLine(true);
        toolbar.AddView(_address, new LinearLayout.LayoutParams(
            0,
            ViewGroup.LayoutParams.WrapContent,
            1));

        var go = CreateButton("GO");
        go.Click += (_, _) => NavigateFromAddress();
        toolbar.AddView(go, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent));

        var clear = CreateButton("CLEAR");
        clear.Click += (_, _) =>
        {
            ClearSessionData(_webView);
            SetStatus("Transient browser data cleared.");
        };
        toolbar.AddView(clear, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent));
        root.AddView(toolbar, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        _status = new TextView(this)
        {
            Text = "PRIVATE SESSION · NO HISTORY",
        };
        _status.SetTextColor(global::Android.Graphics.Color.Rgb(96, 195, 155));
        _status.SetPadding(14, 0, 14, 8);
        root.AddView(_status, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        _webView = new WebView(this);
        ConfigureWebView(_webView);
        root.AddView(_webView, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        SetContentView(root);
        NavigateFromAddress();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is null)
            return;

        var address = intent.GetStringExtra(StartAddressExtra)
            ?? intent.DataString;
        if (!string.IsNullOrWhiteSpace(address) && _address is not null)
        {
            _address.Text = address;
            NavigateFromAddress();
        }
    }

#pragma warning disable CA1422
    public override void OnBackPressed()
    {
        if (_webView?.CanGoBack() == true)
        {
            _webView.GoBack();
            return;
        }

        base.OnBackPressed();
    }
#pragma warning restore CA1422

    protected override void OnDestroy()
    {
        if (_webView is not null)
        {
            ClearSessionData(_webView);
            _webView.StopLoading();
            _webView.Destroy();
            _webView = null;
        }

        base.OnDestroy();
    }

    /// <summary>Clears the process-scoped WebView data used by this activity.</summary>
    public static void ClearSessionData(WebView? webView = null)
    {
        webView?.ClearHistory();
        webView?.ClearCache(true);
        webView?.ClearFormData();

        var cookies = CookieManager.Instance;
        if (cookies is null)
        {
            WebStorage.Instance?.DeleteAllData();
            return;
        }

        cookies.RemoveAllCookies(null!);
        cookies.Flush();
        WebStorage.Instance?.DeleteAllData();
    }

    private Button CreateButton(string label)
    {
        var button = new Button(this)
        {
            Text = label,
        };
        button.SetTextColor(global::Android.Graphics.Color.White);
        return button;
    }

    private void ConfigureWebView(WebView webView)
    {
        var settings = webView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.DatabaseEnabled = true;
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        settings.AllowFileAccessFromFileURLs = false;
        settings.AllowUniversalAccessFromFileURLs = false;
        settings.MixedContentMode = MixedContentHandling.NeverAllow;
        settings.CacheMode = CacheModes.NoCache;
        settings.SetSupportMultipleWindows(false);
        settings.SetGeolocationEnabled(false);
        webView.SetDownloadListener(
            new PrivateBrowserDownloadListener(
                (_, _, _, _, _) => SetStatus(
                    "Downloads are disabled in the private browser.")));
        webView.SetWebViewClient(new PrivateBrowserWebViewClient(
            this,
            address =>
            {
                if (_address is not null && !string.IsNullOrWhiteSpace(address))
                    _address.Text = address;
            },
            _ => SetStatus("PRIVATE SESSION · READY")));
        var cookies = CookieManager.Instance;
        if (cookies is not null)
        {
            cookies.SetAcceptCookie(true);
            cookies.SetAcceptThirdPartyCookies(webView, true);
        }
    }

    private void NavigateFromAddress()
    {
        var value = _address?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!PrivacyBrowserAddress.TryNormalize(value, out var normalized))
        {
            SetStatus("Enter a host or HTTP(S) address, such as example.com.");
            return;
        }

        if (_address is not null)
            _address.Text = normalized;
        _webView?.LoadUrl(normalized);
        SetStatus("PRIVATE SESSION · LOADING");
    }

    private void SetStatus(string message)
    {
        if (_status is not null)
            _status.Text = message;
    }
}
