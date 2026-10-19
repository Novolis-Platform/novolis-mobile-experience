using Android.Content;
using Android.Content.PM;
using Novolis.MobileExperience.Shell;

namespace Novolis.MobileExperience.Shell.Android;

/// <summary>PackageManager-backed application catalog for Android.</summary>
public sealed class AndroidApplicationCatalog : IApplicationCatalog
{
    private readonly Context _context;

    /// <summary>Creates a catalog using the supplied Android context.</summary>
    public AndroidApplicationCatalog(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MobileApplicationEntry>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var packageManager = _context.PackageManager
            ?? throw new InvalidOperationException("Android PackageManager is unavailable.");
        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        var activities = packageManager.QueryIntentActivities(
            intent,
            (PackageInfoFlags)0);
        var entries = activities
            .Where(activity => activity.ActivityInfo is not null)
            .Select(activity =>
            {
                var activityInfo = activity.ActivityInfo!;
                var packageName = activityInfo.PackageName
                    ?? activityInfo.ApplicationInfo?.PackageName
                    ?? "unknown";
                var label = activity.LoadLabel(packageManager)?.ToString();
                return new MobileApplicationEntry
                {
                    PackageName = packageName,
                    DisplayName = string.IsNullOrWhiteSpace(label)
                        ? packageName
                        : label,
                    ActivityName = QualifyActivityName(packageName, activityInfo.Name),
                };
            })
            .Where(entry => entry.ActivityName.Length > 0)
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.PackageName, StringComparer.Ordinal)
            .ThenBy(entry => entry.ActivityName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<MobileApplicationEntry>>(entries);
    }

    /// <inheritdoc />
    public Task<bool> LaunchAsync(
        MobileApplicationEntry application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        cancellationToken.ThrowIfCancellationRequested();

        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        intent.SetComponent(
            new ComponentName(
                application.PackageName,
                QualifyActivityName(application.PackageName, application.ActivityName)));
        intent.AddFlags(ActivityFlags.NewTask);
        _context.StartActivity(intent);
        return Task.FromResult(true);
    }

    private static string QualifyActivityName(string packageName, string? activityName)
    {
        if (string.IsNullOrWhiteSpace(activityName))
            return string.Empty;

        if (activityName.StartsWith(".", StringComparison.Ordinal))
            return packageName + activityName;

        return activityName.Contains('.', StringComparison.Ordinal)
            ? activityName
            : packageName + "." + activityName;
    }
}
