using Novolis.IO.Mobile.Android;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>Decision made for one installed package during sanitization planning.</summary>
public sealed record AndroidCleanupDecision
{
    /// <summary>Package under consideration.</summary>
    public required AndroidInstalledPackage Package { get; init; }

    /// <summary>True when the package is eligible for removal in this plan.</summary>
    public bool Remove { get; init; }

    /// <summary>Human-readable policy explanation.</summary>
    public required string Reason { get; init; }
}
