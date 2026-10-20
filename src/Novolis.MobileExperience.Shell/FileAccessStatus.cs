namespace Novolis.MobileExperience.Shell;

/// <summary>Describes the file access boundary used by the shell.</summary>
public sealed record FileAccessStatus
{
    /// <summary>Whether this platform can open a user-selected file or folder.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Access mechanism, such as Android Storage Access Framework.</summary>
    public required string AccessMode { get; init; }

    /// <summary>Whether the shell keeps a recent-file list.</summary>
    public required bool KeepsRecentHistory { get; init; }

    /// <summary>Short current state shown in the shell.</summary>
    public required string Summary { get; init; }

    /// <summary>Details explaining the access boundary.</summary>
    public required string Detail { get; init; }
}
