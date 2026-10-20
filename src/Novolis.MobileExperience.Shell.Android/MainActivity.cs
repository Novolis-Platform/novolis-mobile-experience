using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

namespace Novolis.MobileExperience.Shell.Android;

[IntentFilter(
    new[] { Intent.ActionMain },
    Categories = new[] { Intent.CategoryHome, Intent.CategoryDefault })]
[Activity(
    Label = "Novolis Mobile Experience",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private bool _initialResumeCompleted;
    private bool _wasBackgrounded;
    private AndroidMobileExperienceServices? _services;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _services = new AndroidMobileExperienceServices(this);
        App.ExperienceServices = _services;
        base.OnCreate(savedInstanceState);
        App.ConfigureExperienceServices(_services);
    }

    /// <inheritdoc />
    protected override void OnResume()
    {
        base.OnResume();
        if (_wasBackgrounded)
        {
            App.NotifySystemHomePressed();
        }

        _initialResumeCompleted = true;
    }

    /// <inheritdoc />
    protected override void OnPause()
    {
        if (_initialResumeCompleted)
        {
            _wasBackgrounded = true;
        }

        base.OnPause();
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        _services?.Dispose();
        _services = null;
        base.OnDestroy();
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent?.Action == Intent.ActionMain
            && intent.Categories?.Contains(Intent.CategoryHome) == true)
        {
            App.NotifySystemHomePressed();
        }
    }
}
