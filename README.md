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
- the shell requests network access only for its user-launched HTTPS browser
  surface and does not require a Google or Lenovo account.

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

The read-only privacy audit records account state, approved/forbidden package
state, active transport, always-on VPN policy, and observed connectivity
probe hostnames without retaining addresses or Wi-Fi names:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- privacy --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\privacy\lenovo-tab-m10-x605f
```

The audit intentionally reports a pending state until the NordVPN account
sign-in, Android VPN consent, and always-on/lockdown choices are completed on
the tablet. Installing NordVPN is not evidence of an active tunnel.

## Audited cleanup

Cleanup is preview-only unless both `--apply` and `--yes` are supplied. The
active Lenovo `Minimal` profile preserves Android framework components, the
Novolis shell, NordVPN, VLC, WebView, keyboard, and network essentials. It
removes only explicitly audited candidates. System-image candidates use
`pm uninstall --user 0`, so the stock image remains available and recovery is
possible with `cmd package install-existing --user 0 <package>`. Unknown
packages remain retained or in the review list. When Android protects a
system-image APK from ADB reads, the apply step records package/version/path
recovery metadata instead of pretending an APK backup exists.

The former conservative profile remains available at
`devices/lenovo-tab-m10-x605f/packages-clean.json`.

Preview the active minimal device policy:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- cleanup --profile d:\novolis\novolis-mobile-experience\devices\lenovo-tab-m10-x605f\packages.json --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\cleanup\preview
```

Apply the reviewed cleanup. The command first sets and verifies the Novolis
shell as HOME, backs up each candidate APK with SHA-256 evidence, and records
before/after inventories before applying the package operations:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- cleanup --profile d:\novolis\novolis-mobile-experience\devices\lenovo-tab-m10-x605f\packages.json --serial HA1417X9 --output-directory d:\novolis\novolis-mobile-experience\artifacts\cleanup\lenovo-tab-m10-x605f --apply --yes
```

## Approved Novolis updates

Release checks run on the Windows provisioning host. The Android shell does not
contact GitHub or run an update worker; its network permission is limited to
the user-launched browser surface. The default is the privacy-preserving
local-mirror policy:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- updates check --serial HA1417X9 --catalog-directory d:\novolis\novolis-mobile-experience\catalog --output-directory d:\novolis\novolis-mobile-experience\artifacts\updates\lenovo-tab-m10-x605f
```

An explicit host-only GitHub check is available when a mirror is not being
used. It never grants the tablet network access:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- updates check --direct-github --include-not-installed --serial HA1417X9 --catalog-directory d:\novolis\novolis-mobile-experience\catalog --output-directory d:\novolis\novolis-mobile-experience\artifacts\updates\lenovo-tab-m10-x605f
```

Installation requires selecting one approved application, explicit direct
GitHub access, and confirmation. The release SHA-256, exact package name,
device ABI, and locally pinned APK signing certificate are verified before
ADB installation:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- updates apply --application merglyph --direct-github --serial HA1417X9 --catalog-directory d:\novolis\novolis-mobile-experience\catalog --output-directory d:\novolis\novolis-mobile-experience\artifacts\updates\lenovo-tab-m10-x605f --yes
```

No APK is installed while its signing identity is unpinned. The Merglyph
release is a legacy asset-only release without `Novolis.Update.json`; its exact
APK basename, GitHub digest, package id, ABI, and signing certificate are
still checked before installation.

The release-backed catalog currently contains every Android APK published by
the Novolis organization release feeds: Novolis Reach, Novolis Sudoku, Novolis
Quiet Video, and Merglyph. They are all marked for automatic maintenance,
including installation when they are absent:

```powershell
dotnet run --project d:\novolis\novolis-mobile-experience\src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj -p:NovolisUseProjectReferences=true -- updates maintain --direct-github --serial HA1417X9 --catalog-directory d:\novolis\novolis-mobile-experience\catalog --output-directory d:\novolis\novolis-mobile-experience\artifacts\updates\lenovo-tab-m10-x605f --yes
```

Maintenance is host-only and must be run by a connected host or an approved
host scheduler. It never grants the Android shell network access. Each APK is
downloaded from a GitHub release asset, checked against the release SHA-256,
package id, device ABI, and pinned signing certificate, then installed only
after all checks pass. The shell itself remains staged until it has a
release-backed Android asset.

The historical Merglyph release feed is private and asset-only. Its host-side
check uses the current user's authenticated GitHub CLI session, or
`GH_TOKEN`/`GITHUB_TOKEN`; no GitHub credential is ever copied to the tablet.

To register a daily host-side maintenance window for the connected tablet:

```powershell
pwsh -File d:\novolis\novolis-mobile-experience\scripts\Register-MobileExperienceMaintenance.ps1 -RepositoryRoot d:\novolis\novolis-mobile-experience -Serial HA1417X9 -At 03:00
```

The scheduled action runs without a visible console window, uses direct GitHub
only on the host, and does nothing when the tablet is not available. Remove it
with the same script and `-Unregister`.

NordVPN is handled as a separately trusted vendor application. The official
NordVPN sideload endpoint currently serves version `6.7.3+sideload`; the
tablet's installed APK is the newer `9.14.1+sideload` build with SHA-256
(`820EC9E0F930504893AA46D4FBE84EDC5A2A7C6617809A284BB2B3163EC7AE4E`) and
the same vendor signing certificate
(`BC64AE0725AF656B3B10B684CD1DF4C9D6B7F81BC5DC32DF3A3B2CE94CE61466`).
NordVPN's release notes advertise a newer Play distribution, but this profile
does not introduce Google Play or install a third-party mirror. The official
sideload endpoint must not be used here until it publishes a build newer than
the installed version; installing its current `6.7.3+sideload` would be a
downgrade.

## Scope boundaries

This repository is an application composition root. It does not publish
NuGet packages. Reusable Android host capabilities remain in existing
Novolis libraries, while device policy, provisioning profiles, recovery, and
the shell stay here.

## Integrated tablet tools

The shell owns the first-party tablet tools as capabilities rather than
launching a separate application for each one:

- **Private browser** — opens a shell-owned Android WebView session. Hostnames
  are upgraded to HTTPS, while cookies and WebView storage are available only
  for the active session so OAuth and sign-in flows can work. Downloads,
  local-file access, and URL history remain disabled; cache, forms, cookies,
  web storage, and navigation history are cleared when the session closes. The
  activity also registers as the device's HTTP(S) browser handler, allowing
  vendor sign-in flows such as NordVPN's to discover and launch it.
- **Files** — opens Android's Storage Access Framework for one file or one
  user-selected folder. The shell does not request broad storage access, keep
  recent paths, or retain a folder grant. Android's picker is the supported
  permission boundary on the target API 28 tablet; a normal application
  cannot silently obtain unrestricted storage. The Android picker may render
  its own system-level locations or recents; those are not collected or
  retained by the shell.
- **Bluetooth** — exposes paired devices and one foreground discovery pass.
  Runtime permissions are requested only when the user starts discovery.
  Pairing and adapter changes remain in Android's system settings flow.
- **System** — shows the observed privacy and capability boundary so requested
  policy is not confused with enforcement.

The Android manifest contains only the permissions needed for these explicit
actions: network access for the browser, the Android 9 location permission
needed for legacy Bluetooth discovery, and the Android 12 nearby-device
permissions for newer builds. No background Bluetooth scan or file-history
worker is installed.
