using Android.App;
using Android.Runtime;
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
        App.ApplicationCatalog = new AndroidApplicationCatalog(this);
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .LogToTrace();
}
