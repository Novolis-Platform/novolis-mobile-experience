using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Desktop;

/// <summary>Deterministic demo catalog used only by the Windows visual prototype.</summary>
public sealed class DesktopDemoApplicationCatalog : IApplicationCatalog
{
    /// <inheritdoc />
    public Task<IReadOnlyList<MobileApplicationEntry>> DiscoverAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MobileApplicationEntry>>(
        [
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.mobileexperience.shell",
                DisplayName = "Novolis Mobile Experience",
                ActivityName = ".MainActivity",
                IsHomeApplication = true,
                InstalledVersion = "0.1.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.merglyph",
                DisplayName = "Merglyph",
                ActivityName = ".MainActivity",
                InstalledVersion = "1.3.0",
                AvailableVersion = "1.4.0",
                UpdateState = MobileUpdateState.Available,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.readaloud",
                DisplayName = "Read Aloud",
                ActivityName = ".MainActivity",
                InstalledVersion = "2.0.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.pdfreader",
                DisplayName = "PDF Reader",
                ActivityName = ".MainActivity",
                InstalledVersion = "1.2.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.books",
                DisplayName = "Books Mobile",
                ActivityName = ".MainActivity",
                InstalledVersion = "0.9.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.quietvideo",
                DisplayName = "Quiet Video",
                ActivityName = ".MainActivity",
                InstalledVersion = "1.0.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "com.novolis.presenceledger",
                DisplayName = "Presence Ledger",
                ActivityName = ".MainActivity",
                InstalledVersion = "0.5.0",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
            new MobileApplicationEntry
            {
                PackageName = "org.videolan.vlc",
                DisplayName = "VLC",
                ActivityName = "org.videolan.vlc.gui.video.VideoPlayerActivity",
                InstalledVersion = "3.7.2",
                UpdateState = MobileUpdateState.Current,
                HasRecentUpdateCheck = true,
            },
        ]);

    /// <inheritdoc />
    public Task<bool> LaunchAsync(
        MobileApplicationEntry application,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(application.CanOpen);
}
