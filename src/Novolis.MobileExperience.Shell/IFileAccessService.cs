namespace Novolis.MobileExperience.Shell;

/// <summary>Opens Android's user-mediated file and folder surfaces without keeping history.</summary>
public interface IFileAccessService
{
    /// <summary>Describes the available file access boundary.</summary>
    FileAccessStatus Status { get; }

    /// <summary>Opens a system picker for one readable file.</summary>
    Task<MobileActionResult> OpenFilePickerAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Opens a system picker for one user-selected folder.</summary>
    Task<MobileActionResult> OpenFolderPickerAsync(
        CancellationToken cancellationToken = default);
}
