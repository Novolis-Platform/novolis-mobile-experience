using Android.App;
using Android.Content;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

/// <summary>Uses Android's Storage Access Framework without retaining file history.</summary>
public sealed class AndroidFileAccessService : IFileAccessService
{
    private const int FileRequestCode = 4101;
    private const int FolderRequestCode = 4102;
    private readonly Activity _activity;

    /// <summary>Creates a file access service bound to the visible shell activity.</summary>
    public AndroidFileAccessService(Activity activity)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
    }

    /// <inheritdoc />
    public FileAccessStatus Status { get; } = new()
    {
        IsAvailable = true,
        AccessMode = "Android Storage Access Framework",
        KeepsRecentHistory = false,
        Summary = "FILE ACCESS READY",
        Detail = "The system picker grants access only to the item or folder you choose; the shell does not retain paths or recent items.",
    };

    /// <inheritdoc />
    public Task<MobileActionResult> OpenFilePickerAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        try
        {
            _activity.StartActivityForResult(intent, FileRequestCode);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MobileActionResult.Failure(
                $"Android could not open the file picker: {ex.Message}"));
        }

        return Task.FromResult(MobileActionResult.Success(
            "Android file picker opened.",
            requiresUserAction: true));
    }

    /// <inheritdoc />
    public Task<MobileActionResult> OpenFolderPickerAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(
            ActivityFlags.GrantReadUriPermission
            | ActivityFlags.GrantWriteUriPermission);
        try
        {
            _activity.StartActivityForResult(intent, FolderRequestCode);
        }
        catch (Exception ex)
        {
            return Task.FromResult(MobileActionResult.Failure(
                $"Android could not open the folder picker: {ex.Message}"));
        }

        return Task.FromResult(MobileActionResult.Success(
            "Android folder picker opened.",
            requiresUserAction: true));
    }
}
