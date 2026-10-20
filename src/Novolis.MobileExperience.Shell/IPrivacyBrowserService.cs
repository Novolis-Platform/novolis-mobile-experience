namespace Novolis.MobileExperience.Shell;

/// <summary>Controls the shell-owned privacy browser wrapper.</summary>
public interface IPrivacyBrowserService
{
    /// <summary>Describes the current browser privacy boundary.</summary>
    PrivacyBrowserStatus Status { get; }

    /// <summary>Opens a validated web address in the browser wrapper.</summary>
    Task<MobileActionResult> OpenAsync(
        string address,
        CancellationToken cancellationToken = default);

    /// <summary>Clears transient browser data retained by the current process.</summary>
    Task<MobileActionResult> ClearSessionAsync(
        CancellationToken cancellationToken = default);
}
