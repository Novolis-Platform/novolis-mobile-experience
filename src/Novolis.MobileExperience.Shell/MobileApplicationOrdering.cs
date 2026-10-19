namespace Novolis.MobileExperience.Shell;

/// <summary>Stable display ordering for the application launcher.</summary>
public static class MobileApplicationOrdering
{
    /// <summary>Returns applications ordered by trimmed display name ascending.</summary>
    public static IReadOnlyList<MobileApplicationEntry> SortForDisplay(
        IEnumerable<MobileApplicationEntry> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        return applications
            .OrderBy(
                application => application.DisplayName.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.PackageName, StringComparer.Ordinal)
            .ThenBy(application => application.ActivityName, StringComparer.Ordinal)
            .ToArray();
    }
}
