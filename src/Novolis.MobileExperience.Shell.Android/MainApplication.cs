using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Webkit;
using Avalonia;
using Avalonia.Android;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

[Application]
public sealed class MainApplication : AvaloniaAndroidApplication<App>
{
    private MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            WebView.SetDataDirectorySuffix("novolis-private");

        App.ApplicationCatalog = new AndroidApplicationCatalog(this);
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .LogToTrace();
}
