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
}
