using Android.Bluetooth;
using Android.Content;

namespace Novolis.MobileExperience.Shell.Android;

internal sealed class AndroidBluetoothDiscoveryReceiver : BroadcastReceiver
{
    public event Action<BluetoothDevice>? DeviceFound;

    public event Action<bool>? DiscoveryStateChanged;

    public override void OnReceive(Context? context, Intent? intent)
    {
        switch (intent?.Action)
        {
            case BluetoothDevice.ActionFound:
#pragma warning disable CA1422
                var device = intent.GetParcelableExtra(
                    BluetoothDevice.ExtraDevice) as BluetoothDevice;
#pragma warning restore CA1422
                if (device is not null)
                    DeviceFound?.Invoke(device);
                break;
            case BluetoothAdapter.ActionDiscoveryStarted:
                DiscoveryStateChanged?.Invoke(true);
                break;
            case BluetoothAdapter.ActionDiscoveryFinished:
                DiscoveryStateChanged?.Invoke(false);
                break;
        }
    }
}
