namespace Novolis.MobileExperience.Provisioning;

/// <summary>Host-side options for a controlled Novolis Android update run.</summary>
public sealed record MobileUpdateOptions
{
    /// <summary>Directory containing applications.json, sources.json, and trust.json.</summary>
    public string CatalogDirectory { get; init; } = "catalog";

    /// <summary>Directory for state, verified downloads, and update evidence.</summary>
    public string OutputDirectory { get; init; } = "artifacts/updates";

    /// <summary>Target Android device serial.</summary>
    public string? Serial { get; init; }

    /// <summary>One application id to check or apply.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Explicitly permits host network access to public GitHub releases.</summary>
    public bool AllowDirectGithub { get; init; }

    /// <summary>Includes approved applications that are not installed yet.</summary>
    public bool IncludeNotInstalled { get; init; }

    /// <summary>Limits the run to catalog applications marked for automatic updates.</summary>
    public bool AutomaticOnly { get; init; }
}
