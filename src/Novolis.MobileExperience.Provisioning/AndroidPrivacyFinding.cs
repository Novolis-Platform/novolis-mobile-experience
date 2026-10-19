namespace Novolis.MobileExperience.Provisioning;

/// <summary>One inspectable privacy or VPN audit finding.</summary>
public sealed record AndroidPrivacyFinding
{
    /// <summary>Stable finding identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Whether the observed state satisfies the profile.</summary>
    public bool Ok { get; init; }

    /// <summary>Human-readable evidence message.</summary>
    public required string Message { get; init; }
}
