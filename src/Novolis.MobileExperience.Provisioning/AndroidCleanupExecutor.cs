using System.Security.Cryptography;
using Novolis.IO.Mobile.Android;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Applies an already reviewed cleanup plan.
/// </summary>
public static class AndroidCleanupExecutor
{
    /// <summary>
    /// Backs up each candidate APK, then removes the package. No package is
    /// changed if any planned APK cannot be backed up.
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
        foreach (var decision in plan.Removals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(decision.Package.ApkPath))
            {
                return new AndroidCleanupExecution
                {
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
                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    Failure =
                        $"Cannot safely remove {decision.Package.PackageName}: " +
                        $"APK backup failed ({backup.Message}).",
                };
            }

            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length == 0)
            {
                return new AndroidCleanupExecution
                {
                    Backups = backups,
                    Failure =
                        $"Cannot safely remove {decision.Package.PackageName}: " +
                        "APK backup was empty.",
                };
            }

            await WriteHashAsync(backupPath, cancellationToken).ConfigureAwait(false);
            backups.Add(backupPath);
        }

        var removedPackages = new List<string>();
        foreach (var decision in plan.Removals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removal = await adb.UninstallAsync(
                    decision.Package.PackageName,
                    serial,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!removal.Ok)
            {
                return new AndroidCleanupExecution
                {
                    Backups = backups,
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
            RemovedPackages = removedPackages,
        };
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
