using Novolis.IO.Mobile.Android;
using Novolis.MobileExperience.Provisioning;

namespace Novolis.MobileExperience.Unit;

public sealed class CleanupPlanTests
{
    [Test]
    public async Task PlanRetainsSystemAndApprovedApplications()
    {
        var profile = new AndroidCleanupProfile
        {
            ProfileId = "test",
            ProtectedPackages = ["android"],
            RetainedPackages = ["com.nordvpn.android", "org.videolan.vlc"],
            CandidateThirdPartyPackages = ["com.example.remove"],
        };
        var inventory = CreateInventory(
            new AndroidInstalledPackage
            {
                PackageName = "android",
                IsSystem = true,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "com.nordvpn.android",
                IsSystem = false,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "org.videolan.vlc",
                IsSystem = false,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "com.example.remove",
                IsSystem = false,
                IsEnabled = true,
            });

        var plan = AndroidCleanupPlan.Create(inventory, profile);

        await Assert.That(plan.Removals.Select(
                decision => decision.Package.PackageName))
            .Contains("com.example.remove");
        await Assert.That(plan.Removals).Count().IsEqualTo(1);
        await Assert.That(plan.Decisions.Where(
                decision => decision.Package.PackageName == "android"
                    || decision.Package.PackageName == "com.nordvpn.android"
                    || decision.Package.PackageName == "org.videolan.vlc")
                .All(decision => !decision.Remove))
            .IsTrue();
    }

    [Test]
    public async Task UnknownNonSystemPackageIsHeldForReview()
    {
        var profile = new AndroidCleanupProfile
        {
            ProfileId = "test",
            CandidateThirdPartyPackages = ["com.example.remove"],
        };
        var inventory = CreateInventory(
            new AndroidInstalledPackage
            {
                PackageName = "com.example.unknown",
                IsSystem = false,
                IsEnabled = true,
            });

        var plan = AndroidCleanupPlan.Create(inventory, profile);

        await Assert.That(plan.Removals).Count().IsEqualTo(0);
        await Assert.That(plan.ReviewRequired.Select(
                decision => decision.Package.PackageName))
            .Contains("com.example.unknown");
    }

    [Test]
    public async Task ExplicitSystemCandidateUsesReversibleUserRemoval()
    {
        var profile = new AndroidCleanupProfile
        {
            ProfileId = "test",
            ProtectedPackages = ["com.google.android.webview", "com.android.systemui"],
            CandidateSystemPackages = ["com.google.android.gms"],
        };
        var inventory = CreateInventory(
            new AndroidInstalledPackage
            {
                PackageName = "com.google.android.gms",
                IsSystem = true,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "com.google.android.webview",
                IsSystem = true,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "com.android.systemui",
                IsSystem = true,
                IsEnabled = true,
            },
            new AndroidInstalledPackage
            {
                PackageName = "com.example.unknown-system",
                IsSystem = true,
                IsEnabled = true,
            });

        var plan = AndroidCleanupPlan.Create(inventory, profile);

        var removal = plan.Removals.Single();
        await Assert.That(removal.Package.PackageName)
            .IsEqualTo("com.google.android.gms");
        await Assert.That(removal.Operation).IsEqualTo("remove-for-user");
        await Assert.That(plan.Decisions
                .Where(decision =>
                    decision.Package.PackageName == "com.google.android.webview"
                    || decision.Package.PackageName == "com.android.systemui"
                    || decision.Package.PackageName == "com.example.unknown-system")
                .All(decision => !decision.Remove))
            .IsTrue();
    }

    [Test]
    public async Task SystemCandidateDoesNotRemoveNonSystemPackage()
    {
        var profile = new AndroidCleanupProfile
        {
            ProfileId = "test",
            CandidateSystemPackages = ["com.example.package"],
        };
        var inventory = CreateInventory(
            new AndroidInstalledPackage
            {
                PackageName = "com.example.package",
                IsSystem = false,
                IsEnabled = true,
            });

        var plan = AndroidCleanupPlan.Create(inventory, profile);

        await Assert.That(plan.Removals).Count().IsEqualTo(0);
        await Assert.That(plan.ReviewRequired.Select(
                decision => decision.Package.PackageName))
            .Contains("com.example.package");
    }

    private static AndroidDeviceInventory CreateInventory(
        params AndroidInstalledPackage[] packages) =>
        new()
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Device = new AndroidDeviceInfo
            {
                Serial = "test",
                State = "device",
            },
            Packages = packages,
        };
}
