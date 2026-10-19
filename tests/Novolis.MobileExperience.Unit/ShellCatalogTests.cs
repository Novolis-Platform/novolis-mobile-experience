using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Unit;

public sealed class ShellCatalogTests
{
    [Test]
    public async Task EmptyCatalogIsSafeForNonAndroidHosts()
    {
        var catalog = new EmptyApplicationCatalog();

        var applications = await catalog.DiscoverAsync();
        var launched = await catalog.LaunchAsync(
            new MobileApplicationEntry
            {
                PackageName = "com.example.none",
                DisplayName = "Nothing",
                ActivityName = ".MainActivity",
            });

        await Assert.That(applications).Count().IsEqualTo(0);
        await Assert.That(launched).IsFalse();
    }
}
