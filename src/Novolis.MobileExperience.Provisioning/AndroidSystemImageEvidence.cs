namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Recovery metadata recorded when a system-image APK cannot be pulled by
/// the ADB shell user before reversible user-0 removal.
/// </summary>
public sealed record AndroidSystemImageEvidence
{
    /// <summary>Package that remains in the stock system image.</summary>
    public required string PackageName { get; init; }

    /// <summary>APK path reported by PackageManager, when available.</summary>
    public string? ApkPath { get; init; }

    /// <summary>Package version code observed during inventory.</summary>
    public long? VersionCode { get; init; }

    /// <summary>Reason a local APK copy was unavailable.</summary>
    public required string BackupFailure { get; init; }

    /// <summary>Recovery command for Android user 0.</summary>
    public string RecoveryCommand =>
        $"cmd package install-existing --user 0 {PackageName}";
}
