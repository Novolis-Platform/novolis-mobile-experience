# Novolis Mobile Experience

Novolis Mobile Experience is the controlled Android shell, application
catalog, device-control surface, and provisioning workflow for Novolis
tablets.

The first slice is deliberately read-only on the device:

- the reusable `Novolis.IO.Mobile.Android` inventory API collects device,
  package, launcher, account, and selected privacy settings;
- `novolis-android inventory` writes a redacted evidence bundle unless
  `--raw` is explicitly supplied;
- the provisioning host can collect the same inventory into a controlled
  artifact directory;
- the Avalonia shell enumerates Android launcher activities and opens an
  application through the normal Android intent path;
- the Device Control APK is a separate, network-free Android component
  reserved for maintenance and installation orchestration;
- the shell requests no network permission and does not require a Google or
  Lenovo account.

## Build

Restore and build the Android shell from the repository root:

```powershell
dotnet build d:\novolis\novolis-mobile-experience\Novolis.MobileExperience.slnx -p:NovolisUseProjectReferences=true
```

The local workspace build uses ProjectReference mode for Novolis package
dependencies. Committed projects retain `LibraryReference` items and CI
restores approved Novolis packages from GitHub Packages.

The solution contains:

- `Novolis.MobileExperience.Provisioning` — Windows-hosted read-only
  inventory entry point;
- `Novolis.MobileExperience.Shell.Android` — minimal Avalonia launcher;
- `Novolis.MobileExperience.DeviceControl` — separate Android control
  component with no launcher UI yet.

## Connected-device inventory

The general-purpose Android tool owns ADB operations:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Android.Cli\Novolis.Tools.Android.Cli.csproj -p:NovolisUseProjectReferences=true -- inventory d:\novolis\novolis-mobile-experience\artifacts\inventory --serial HA1417X9
```

This command does not install, disable, clear, or remove anything.

The repository-local provisioning entry point has the same safe boundary:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- inventory --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\inventory
```

## Audited cleanup

Cleanup is preview-only unless both `--apply` and `--yes` are supplied. The
Lenovo profile preserves every system-image package, the Novolis shell,
NordVPN, and VLC. It removes only the five non-system packages explicitly
listed as audited candidates in `devices/lenovo-tab-m10-x605f/packages.json`.
Unknown non-system packages remain in the review list.

Preview the current device policy:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- cleanup --profile d:\novolis\novolis-mobile-experience\devices\lenovo-tab-m10-x605f\packages.json --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\cleanup\preview
```

Apply the reviewed cleanup. The command backs up each candidate APK and
records before/after inventories before removing packages:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- cleanup --profile d:\novolis\novolis-mobile-experience\devices\lenovo-tab-m10-x605f\packages.json --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\cleanup\lenovo-tab-m10-x605f --apply --yes
```

## Scope boundaries

This repository is an application composition root. It does not publish
NuGet packages. Reusable Android host capabilities remain in existing
Novolis libraries, while device policy, provisioning profiles, recovery, and
the shell stay here.
