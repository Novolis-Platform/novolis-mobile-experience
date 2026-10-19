namespace Novolis.MobileExperience.Provisioning;

/// <summary>Result for one approved Android application in an update run.</summary>
public sealed record MobileUpdateApplicationReport
{
    /// <summary>Catalog application identifier.</summary>
    public required string ApplicationId { get; init; }

    /// <summary>Display name from the catalog.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Android package name.</summary>
    public required string PackageName { get; init; }

    /// <summary>Version currently reported by PackageManager or the catalog floor.</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>Stable state such as current, available, not-installed, or blocked.</summary>
    public required string State { get; init; }

    /// <summary>Candidate version selected from the release manifest.</summary>
    public string? AvailableVersion { get; init; }

    /// <summary>Release asset selected by the registry source.</summary>
    public string? ArtifactName { get; init; }

    /// <summary>SHA-256 digest declared by the release manifest.</summary>
    public string? ArtifactSha256 { get; init; }

    /// <summary>Release page for the selected candidate.</summary>
    public Uri? ReleaseUri { get; init; }

    /// <summary>Whether the trust catalog pins a signing identity for installation.</summary>
    public bool TrustedForInstall { get; init; }

    /// <summary>Verified local download path, when an apply run completed download.</summary>
    public string? DownloadPath { get; init; }

    /// <summary>Failure or blocking code.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Failure or blocking message.</summary>
    public string? ErrorMessage { get; init; }
}
