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
