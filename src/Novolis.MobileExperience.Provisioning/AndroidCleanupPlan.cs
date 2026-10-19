using Novolis.IO.Mobile.Android;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>Safe, inspectable package cleanup plan.</summary>
public sealed record AndroidCleanupPlan
{
    /// <summary>Profile used to produce the plan.</summary>
    public required AndroidCleanupProfile Profile { get; init; }

    /// <summary>All package decisions, including retained system packages.</summary>
    public IReadOnlyList<AndroidCleanupDecision> Decisions { get; init; } = [];

    /// <summary>Packages eligible for the apply phase.</summary>
    public IReadOnlyList<AndroidCleanupDecision> Removals =>
        Decisions.Where(decision => decision.Remove).ToArray();

    /// <summary>Non-system packages intentionally held for review.</summary>
    public IReadOnlyList<AndroidCleanupDecision> ReviewRequired =>
        Decisions
            .Where(decision =>
                !decision.Package.IsSystem
                && !decision.Remove
                && !IsRetained(decision.Package.PackageName))
            .ToArray();

    /// <summary>Creates a plan without changing the device.</summary>
    public static AndroidCleanupPlan Create(
        AndroidDeviceInventory inventory,
        AndroidCleanupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(profile);

        var protectedPackages = ToSet(profile.ProtectedPackages);
        var retainedPackages = ToSet(profile.RetainedPackages);
        retainedPackages.UnionWith(profile.RetainUntilCompatibilityTested);
        var candidates = ToSet(profile.CandidateThirdPartyPackages);

        var decisions = inventory.Packages
            .OrderBy(package => package.PackageName, StringComparer.Ordinal)
            .Select(package =>
            {
                if (package.IsSystem)
                {
                    return new AndroidCleanupDecision
                    {
                        Package = package,
                        Reason = "System-image package is protected by default.",
                    };
                }

                if (protectedPackages.Contains(package.PackageName))
                {
                    return new AndroidCleanupDecision
                    {
                        Package = package,
                        Reason = "Explicitly protected by the device profile.",
                    };
                }

                if (retainedPackages.Contains(package.PackageName))
                {
                    return new AndroidCleanupDecision
                    {
                        Package = package,
                        Reason = "Explicitly retained by the device profile.",
                    };
                }

                if (candidates.Contains(package.PackageName))
                {
                    return new AndroidCleanupDecision
                    {
                        Package = package,
                        Remove = true,
                        Reason = "Audited non-system package is an approved cleanup candidate.",
                    };
                }

                return new AndroidCleanupDecision
                {
                    Package = package,
                    Reason = "Unknown non-system package is held for review.",
                };
            })
            .ToArray();

        return new AndroidCleanupPlan
        {
            Profile = profile,
            Decisions = decisions,
        };
    }

    private bool IsRetained(string packageName) =>
        Profile.ProtectedPackages.Contains(packageName, StringComparer.Ordinal)
        || Profile.RetainedPackages.Contains(packageName, StringComparer.Ordinal)
        || Profile.RetainUntilCompatibilityTested.Contains(packageName, StringComparer.Ordinal);

    private static HashSet<string> ToSet(IEnumerable<string> packages) =>
        packages
            .Where(package => !string.IsNullOrWhiteSpace(package))
            .ToHashSet(StringComparer.Ordinal);
}
