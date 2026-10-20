using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

/// <summary>Provides foreground-only Bluetooth management through Android APIs.</summary>
public sealed class AndroidBluetoothService : IBluetoothService, IDisposable
{
    private const int PermissionRequestCode = 4201;
    private const int EnableRequestCode = 4202;
    private readonly Activity _activity;
    private readonly BluetoothAdapter? _adapter;
    private readonly AndroidBluetoothDiscoveryReceiver _receiver;
    private readonly Dictionary<string, BluetoothDevice> _discovered = new(
        StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>Creates a Bluetooth service bound to the visible shell activity.</summary>
    public AndroidBluetoothService(Activity activity)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        var manager = _activity.GetSystemService(Context.BluetoothService)
            as BluetoothManager;
        _adapter = manager?.Adapter;
        _receiver = new AndroidBluetoothDiscoveryReceiver();
        _receiver.DeviceFound += OnDeviceFound;
        RegisterDiscoveryReceiver();
    }

    /// <inheritdoc />
    public Task<BluetoothSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adapter is null)
        {
            return Task.FromResult(BluetoothSnapshot.Unavailable(
                "This tablet does not expose a Bluetooth adapter."));
        }

        var missing = GetMissingPermissions();
        if (missing.Count > 0)
        {
            return Task.FromResult(new BluetoothSnapshot
            {
                Availability = BluetoothAvailability.PermissionRequired,
                IsEnabled = false,
                IsDiscovering = false,
                Devices = [],
                Summary = "BLUETOOTH ACCESS NEEDED",
                Detail = "Android requires a user-approved nearby-device permission before discovery can begin.",
            });
        }

        try
        {
            var devices = new Dictionary<string, BluetoothDeviceEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var device in _adapter.BondedDevices ?? [])
                AddDevice(devices, device, isPaired: true, wasDiscovered: false);

            foreach (var device in _discovered.Values)
                AddDevice(devices, device, isPaired: false, wasDiscovered: true);

            var isEnabled = _adapter.IsEnabled;
            var isDiscovering = _adapter.IsDiscovering;
            return Task.FromResult(new BluetoothSnapshot
            {
                Availability = isEnabled
                    ? BluetoothAvailability.Ready
                    : BluetoothAvailability.Disabled,
                IsEnabled = isEnabled,
                IsDiscovering = isDiscovering,
                Devices = devices.Values
                    .OrderByDescending(device => device.IsPaired)
                    .ThenBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Summary = !isEnabled
                    ? "BLUETOOTH OFF"
                    : isDiscovering
                        ? "SCANNING NEARBY"
                        : "BLUETOOTH READY",
                Detail = isEnabled
                    ? $"{devices.Values.Count(device => device.IsPaired)} paired · {_discovered.Count} found in this scan."
                    : "Enable Bluetooth in Android settings to pair or discover devices.",
            });
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(new BluetoothSnapshot
            {
                Availability = BluetoothAvailability.PermissionRequired,
                IsEnabled = false,
                IsDiscovering = false,
                Devices = [],
                Summary = "BLUETOOTH ACCESS NEEDED",
                Detail = "Android did not grant access to the adapter. Approve the permission and refresh.",
            });
        }
    }

    /// <inheritdoc />
    public Task<MobileActionResult> RequestPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var missing = GetMissingPermissions();
        if (missing.Count == 0)
        {
            return Task.FromResult(MobileActionResult.Success(
                "Bluetooth access is already allowed."));
        }

        _activity.RequestPermissions(missing.ToArray(), PermissionRequestCode);
        return Task.FromResult(MobileActionResult.Success(
            "Android permission prompt opened.",
            requiresUserAction: true));
    }

    /// <inheritdoc />
    public Task<MobileActionResult> StartDiscoveryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adapter is null)
            return Task.FromResult(MobileActionResult.Failure(
                "This tablet does not expose a Bluetooth adapter."));

        var missing = GetMissingPermissions();
        if (missing.Count > 0)
            return Task.FromResult(MobileActionResult.Failure(
                "Allow Bluetooth access before scanning."));

        try
        {
            if (!_adapter.IsEnabled)
            {
                var enable = new Intent(BluetoothAdapter.ActionRequestEnable);
                try
                {
                    _activity.StartActivityForResult(enable, EnableRequestCode);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(MobileActionResult.Failure(
                        $"Android could not open the Bluetooth enable prompt: {ex.Message}"));
                }

                return Task.FromResult(MobileActionResult.Success(
                    "Android asked to enable Bluetooth.",
                    requiresUserAction: true));
            }

            if (_adapter.IsDiscovering)
                return Task.FromResult(MobileActionResult.Success(
                    "Bluetooth discovery is already running."));

            _discovered.Clear();
            var started = _adapter.StartDiscovery();
            return Task.FromResult(
                started
                    ? MobileActionResult.Success("Bluetooth discovery started.")
                    : MobileActionResult.Failure("Android could not start Bluetooth discovery."));
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(MobileActionResult.Failure(
                "Android denied Bluetooth discovery."));
        }
    }

    /// <inheritdoc />
    public Task<MobileActionResult> StopDiscoveryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adapter is null)
            return Task.FromResult(MobileActionResult.Success(
                "No Bluetooth adapter is available."));

        try
        {
            if (_adapter.IsDiscovering)
                _adapter.CancelDiscovery();
            return Task.FromResult(MobileActionResult.Success(
                "Bluetooth discovery stopped."));
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(MobileActionResult.Failure(
                "Android denied stopping Bluetooth discovery."));
        }
    }

    /// <inheritdoc />
    public Task<MobileActionResult> PairAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adapter is null)
            return Task.FromResult(MobileActionResult.Failure(
                "This tablet does not expose a Bluetooth adapter."));

        if (GetMissingPermissions().Count > 0)
            return Task.FromResult(MobileActionResult.Failure(
                "Allow Bluetooth access before pairing."));

        if (string.IsNullOrWhiteSpace(address)
            || !_discovered.TryGetValue(address, out var device))
        {
            return Task.FromResult(MobileActionResult.Failure(
                "The device is no longer in the current discovery set."));
        }

        try
        {
            if (device.BondState == Bond.Bonded)
            {
                return Task.FromResult(MobileActionResult.Success(
                    "The device is already paired."));
            }

            var started = device.CreateBond();
            return Task.FromResult(
                started
                    ? MobileActionResult.Success(
                        "Android pairing flow started.",
                        requiresUserAction: true)
                    : MobileActionResult.Failure(
                        "Android could not start pairing for this device."));
        }
        catch (Java.Lang.SecurityException)
        {
            return Task.FromResult(MobileActionResult.Failure(
                "Android denied the pairing request."));
        }
    }

    /// <inheritdoc />
    public Task<MobileActionResult> OpenSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var intent = new Intent(
            global::Android.Provider.Settings.ActionBluetoothSettings);
        try
        {
            _activity.StartActivity(intent);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MobileActionResult.Failure(
                $"Android could not open Bluetooth settings: {ex.Message}"));
        }

        return Task.FromResult(MobileActionResult.Success(
            "Android Bluetooth settings opened.",
            requiresUserAction: true));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _receiver.DeviceFound -= OnDeviceFound;
        try
        {
            _activity.UnregisterReceiver(_receiver);
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            // The activity may already have released its receiver registry.
        }
    }

    private List<string> GetMissingPermissions()
    {
        var permissions = Build.VERSION.SdkInt >= BuildVersionCodes.S
            ? new[]
            {
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_CONNECT",
            }
            : new[]
            {
                Manifest.Permission.AccessFineLocation,
            };
        return permissions
            .Where(permission =>
                _activity.CheckSelfPermission(permission) != Permission.Granted)
            .ToList();
    }

    private void OnDeviceFound(BluetoothDevice device)
    {
        try
        {
            var address = device.Address;
            if (!string.IsNullOrWhiteSpace(address))
                _discovered[address] = device;
        }
        catch (Java.Lang.SecurityException)
        {
            // The next snapshot will report the missing platform permission.
        }
    }

    private static void AddDevice(
        IDictionary<string, BluetoothDeviceEntry> devices,
        BluetoothDevice device,
        bool isPaired,
        bool wasDiscovered)
    {
        try
        {
            var address = device.Address;
            if (string.IsNullOrWhiteSpace(address))
                return;

            var name = device.Name;
            devices[address] = new BluetoothDeviceEntry
            {
                Address = address,
                DisplayName = string.IsNullOrWhiteSpace(name)
                    ? "Unnamed Bluetooth device"
                    : name,
                IsPaired = isPaired,
                WasDiscovered = wasDiscovered,
                Kind = device.Type.ToString(),
            };
        }
        catch (Java.Lang.SecurityException)
        {
            // Permission state is surfaced by the outer snapshot operation.
        }
    }

    private static IntentFilter BuildDiscoveryFilter()
    {
        var filter = new IntentFilter();
        filter.AddAction(BluetoothDevice.ActionFound);
        filter.AddAction(BluetoothAdapter.ActionDiscoveryStarted);
        filter.AddAction(BluetoothAdapter.ActionDiscoveryFinished);
        return filter;
    }

    private void RegisterDiscoveryReceiver()
    {
        var filter = BuildDiscoveryFilter();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416
            _activity.RegisterReceiver(
                _receiver,
                filter,
                ReceiverFlags.NotExported);
#pragma warning restore CA1416
            return;
        }

        _activity.RegisterReceiver(_receiver, filter);
    }
}
