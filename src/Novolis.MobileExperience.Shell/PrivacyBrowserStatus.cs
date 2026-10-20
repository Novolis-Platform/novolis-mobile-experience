namespace Novolis.MobileExperience.Shell;

/// <summary>Privacy properties exposed by the shell's browser wrapper.</summary>
public sealed record PrivacyBrowserStatus
{
    /// <summary>Whether this platform can open the browser wrapper.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Implementation label, such as an isolated Android WebView.</summary>
    public required string Mode { get; init; }

    /// <summary>Short current state shown in the shell.</summary>
    public required string Summary { get; init; }

    /// <summary>Details explaining the privacy boundary.</summary>
    public required string Detail { get; init; }

    /// <summary>Whether the wrapper retains navigation history between sessions.</summary>
    public required bool StoresHistory { get; init; }
}
