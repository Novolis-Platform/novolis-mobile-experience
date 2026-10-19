using System.Security.Cryptography;
using System.Text.Json;
using Novolis.IO.Mobile.Android;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Applies an already reviewed cleanup plan.
/// </summary>
public static class AndroidCleanupExecutor
{
    /// <summary>
    /// Backs up each candidate APK, then applies the planned operation.
    /// User-installed packages require a local APK backup. System-image
    /// packages may use recovery metadata when the ADB shell cannot read the
    /// immutable image path.
    /// </summary>
    public static async Task<AndroidCleanupExecution> ExecuteAsync(
        AndroidDebugBridge adb,
        AndroidCleanupPlan plan,
        string outputDirectory,
        string? serial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adb);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var backupDirectory = Path.Combine(
            Path.GetFullPath(outputDirectory),
            "apk-backup");
        Directory.CreateDirectory(backupDirectory);

        var backups = new List<string>();
        var systemImageEvidence = new List<AndroidSystemImageEvidence>();
        var removedDeviceAdminComponents = new List<string>();
        foreach (var decision in plan.Removals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(decision.Package.ApkPath))
            {
                if (CanUseSystemImageRecovery(decision))
                {
                    systemImageEvidence.Add(
                        await WriteSystemImageEvidenceAsync(
                                backupDirectory,
                                decision,
                                "PackageManager did not report an APK path.",
                                cancellationToken)
                            .ConfigureAwait(false));
                    continue;
                }

                return new AndroidCleanupExecution
                {
                    SystemImageEvidence = systemImageEvidence,
                    RemovedDeviceAdminComponents = removedDeviceAdminComponents,
                    Failure =
                        $"Cannot safely remove {decision.Package.PackageName}: " +
                        "PackageManager did not report an APK path.",
                };
            }

            var backupPath = Path.Combine(
                backupDirectory,
                $"{decision.Package.PackageName}.apk");
            var backup = await adb.PullAsync(
                    decision.Package.ApkPath,
                    backupPath,
                    serial,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!backup.Ok || !File.Exists(backupPath))
            {
                if (CanUseSystemImageRecovery(decision))
                {
                    systemImageEvidence.Add(
                        await WriteSystemImageEvidenceAsync(
                                backupDirectory,
                                decision,
                                backup.Message,
                                cancellationToken)
                            .ConfigureAwait(false));
                    continue;
                }

                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    SystemImageEvidence = systemImageEvidence,
                    RemovedDeviceAdminComponents = removedDeviceAdminComponents,
                    Failure =
                        $"Cannot safely remove {decision.Package.PackageName}: " +
                        $"APK backup failed ({backup.Message}).",
                };
            }

            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length == 0)
            {
                if (CanUseSystemImageRecovery(decision))
                {
                    systemImageEvidence.Add(
                        await WriteSystemImageEvidenceAsync(
                                backupDirectory,
                                decision,
                                "APK backup was empty.",
                                cancellationToken)
                            .ConfigureAwait(false));
                    continue;
                }

                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    SystemImageEvidence = systemImageEvidence,
                    RemovedDeviceAdminComponents = removedDeviceAdminComponents,
                    Failure =
                        $"Cannot safely remove {decision.Package.PackageName}: " +
                        "APK backup was empty.",
                };
            }

            await WriteHashAsync(backupPath, cancellationToken).ConfigureAwait(false);
            backups.Add(backupPath);
        }

        foreach (var component in plan.Profile.ActiveDeviceAdminComponentsToRemove)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deviceAdminRemoval = await RemoveActiveDeviceAdminAsync(
                    adb,
                    component,
                    serial,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deviceAdminRemoval.Ok)
            {
                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    SystemImageEvidence = systemImageEvidence,
                    RemovedDeviceAdminComponents = removedDeviceAdminComponents,
                    Failure = deviceAdminRemoval.Message,
                };
            }

            if (deviceAdminRemoval.Message.StartsWith(
                    "Removed ",
                    StringComparison.Ordinal))
            {
                removedDeviceAdminComponents.Add(component);
            }
        }

        var removedPackages = new List<string>();
        foreach (var decision in plan.Removals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removal = decision.Operation switch
            {
                "uninstall" => await adb.UninstallAsync(
                        decision.Package.PackageName,
                        serial,
                        cancellationToken)
                    .ConfigureAwait(false),
                "remove-for-user" => await RemoveForUserAsync(
                        adb,
                        decision.Package.PackageName,
                        serial,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => AdbOperationResult.Fail(
                    "package-removal",
                    $"Unsupported cleanup operation '{decision.Operation}'."),
            };
            if (!removal.Ok)
            {
                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    SystemImageEvidence = systemImageEvidence,
                    RemovedDeviceAdminComponents = removedDeviceAdminComponents,
                    RemovedPackages = removedPackages,
                    Failure =
                        $"Package removal failed for {decision.Package.PackageName}: " +
                        removal.Message,
                };
            }

            removedPackages.Add(decision.Package.PackageName);
        }

        return new AndroidCleanupExecution
        {
            Ok = true,
            Backups = backups,
            SystemImageEvidence = systemImageEvidence,
            RemovedDeviceAdminComponents = removedDeviceAdminComponents,
            RemovedPackages = removedPackages,
        };
    }

    private static bool CanUseSystemImageRecovery(AndroidCleanupDecision decision) =>
        decision.Package.IsSystem
        && string.Equals(
            decision.Operation,
            "remove-for-user",
            StringComparison.Ordinal);

    private static async Task<AndroidSystemImageEvidence> WriteSystemImageEvidenceAsync(
        string backupDirectory,
        AndroidCleanupDecision decision,
        string failure,
        CancellationToken cancellationToken)
    {
        var evidence = new AndroidSystemImageEvidence
        {
            PackageName = decision.Package.PackageName,
            ApkPath = decision.Package.ApkPath,
            VersionCode = decision.Package.VersionCode,
            BackupFailure = failure,
        };
        var evidencePath = Path.Combine(
            backupDirectory,
            $"{decision.Package.PackageName}.system-image.json");
        await File.WriteAllTextAsync(
                evidencePath,
                JsonSerializer.Serialize(
                    evidence,
                    new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteHashAsync(evidencePath, cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private static async Task<AdbOperationResult> RemoveForUserAsync(
        AndroidDebugBridge adb,
        string packageName,
        string? serial,
        CancellationToken cancellationToken)
    {
        var result = await adb.ShellAsync(
                $"pm uninstall --user 0 {packageName}",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok && ContainsDevicePolicyRemovalError(result))
        {
            var disable = await adb.ShellAsync(
                    $"pm disable-user --user 0 {packageName}",
                    serial,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!disable.Ok
                || !disable.StdOut.Contains("disabled-user", StringComparison.OrdinalIgnoreCase))
            {
                return AdbOperationResult.Fail(
                    "remove-for-user",
                    $"Android policy blocked removal of {packageName}, and " +
                    $"disabling it failed: {disable.Diagnostic}",
                    disable);
            }

            result = await adb.ShellAsync(
                    $"pm uninstall --user 0 {packageName}",
                    serial,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var succeeded = result.Ok
            && string.Equals(result.StdOut.Trim(), "Success", StringComparison.OrdinalIgnoreCase);
        return succeeded
            ? AdbOperationResult.Success(
                "remove-for-user",
                $"Removed {packageName} for Android user 0.",
                result)
            : AdbOperationResult.Fail(
                "remove-for-user",
                $"pm uninstall --user 0 did not return Success for {packageName}: " +
                (result.Diagnostic.Length == 0 ? "no diagnostic output" : result.Diagnostic),
                result);
    }

    private static bool ContainsDevicePolicyRemovalError(AdbProcessResult result) =>
        result.StdOut.Contains(
            "DELETE_FAILED_DEVICE_POLICY_MANAGER",
            StringComparison.OrdinalIgnoreCase)
        || result.StdErr.Contains(
            "DELETE_FAILED_DEVICE_POLICY_MANAGER",
            StringComparison.OrdinalIgnoreCase);

    private static bool ContainsDevicePolicyNonTestAdminError(AdbProcessResult result) =>
        result.StdOut.Contains("non-test admin", StringComparison.OrdinalIgnoreCase)
        || result.StdErr.Contains("non-test admin", StringComparison.OrdinalIgnoreCase);

    private static async Task<AdbOperationResult> RemoveActiveDeviceAdminAsync(
        AndroidDebugBridge adb,
        string component,
        string? serial,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(component)
            || component.Any(char.IsWhiteSpace)
            || component.Count(character => character == '/') != 1)
        {
            return AdbOperationResult.Fail(
                "remove-active-admin",
                $"Invalid device-admin component in cleanup profile: {component}");
        }

        var before = await adb.ShellAsync(
                "dumpsys device_policy",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!before.Ok)
        {
            return AdbOperationResult.Fail(
                "remove-active-admin",
                $"Could not inspect device-admin state before removing {component}: " +
                before.Diagnostic,
                before);
        }

        if (!before.StdOut.Contains(component, StringComparison.Ordinal))
        {
            return AdbOperationResult.Success(
                "remove-active-admin",
                $"No active device admin found for {component}.",
                before);
        }

        var removal = await adb.ShellAsync(
                $"dpm remove-active-admin --user 0 {component}",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!removal.Ok)
        {
            if (ContainsDevicePolicyNonTestAdminError(removal))
            {
                return AdbOperationResult.Success(
                    "remove-active-admin",
                    $"Active device admin {component} is non-test; " +
                    "package removal will disable it before retrying.",
                    removal);
            }

            return AdbOperationResult.Fail(
                "remove-active-admin",
                $"Could not remove active device admin {component}: " +
                removal.Diagnostic,
                removal);
        }

        var after = await adb.ShellAsync(
                "dumpsys device_policy",
                serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (!after.Ok || after.StdOut.Contains(component, StringComparison.Ordinal))
        {
            return AdbOperationResult.Fail(
                "remove-active-admin",
                $"Device admin {component} remained active after removal.",
                after);
        }

        return AdbOperationResult.Success(
            "remove-active-admin",
            $"Removed {component}.",
            removal);
    }

    private static async Task WriteHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                path + ".sha256",
                Convert.ToHexString(hash),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
