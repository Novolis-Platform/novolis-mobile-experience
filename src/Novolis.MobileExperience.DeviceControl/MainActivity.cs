using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Novolis.MobileExperience.DeviceControl;

[Activity(
    Label = "Novolis Device Control",
    Exported = false,
    MainLauncher = false,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.Density)]
public sealed class MainActivity : Activity
{
}
