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

    [Test]
    public async Task HomeApplicationDoesNotExposeAnOpenAction()
    {
        var home = new MobileApplicationEntry
        {
            PackageName = "com.novolis.mobileexperience.shell",
            DisplayName = "Novolis Mobile Experience",
            ActivityName = ".MainActivity",
            IsHomeApplication = true,
        };
        var application = home with
        {
            IsHomeApplication = false,
            PackageName = "org.videolan.vlc",
        };

        await Assert.That(home.CanOpen).IsFalse();
        await Assert.That(application.CanOpen).IsTrue();
        await Assert.That(home.IsNovolisApplication).IsTrue();
        await Assert.That(application.IsNovolisApplication).IsFalse();
    }

    [Test]
    public async Task ApplicationsAreOrderedByDisplayNameAscending()
    {
        var applications = MobileApplicationOrdering.SortForDisplay(
        [
            new MobileApplicationEntry
            {
                PackageName = "com.example.sudoku",
                DisplayName = "Sudoku",
                ActivityName = ".MainActivity",
            },
            new MobileApplicationEntry
            {
                PackageName = "com.example.settings",
                DisplayName = "Settings",
                ActivityName = ".MainActivity",
            },
            new MobileApplicationEntry
            {
                PackageName = "com.example.calculator",
                DisplayName = " Calculator ",
                ActivityName = ".MainActivity",
            },
        ]);

        await Assert.That(applications[0].DisplayName).IsEqualTo(" Calculator ");
        await Assert.That(applications[1].DisplayName).IsEqualTo("Settings");
        await Assert.That(applications[2].DisplayName).IsEqualTo("Sudoku");
    }
}
