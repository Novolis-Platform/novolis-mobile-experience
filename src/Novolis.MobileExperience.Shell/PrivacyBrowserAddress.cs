namespace Novolis.MobileExperience.Shell;

/// <summary>Normalizes user-entered addresses for the private browser.</summary>
public static class PrivacyBrowserAddress
{
    /// <summary>
    /// Accepts a host or an HTTP(S) address and returns a valid absolute HTTPS URI.
    /// </summary>
    public static bool TryNormalize(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate["http://".Length..];
        }
        else if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(
                candidate,
                UriKind.Absolute,
                out var address)
            || address.Scheme is not "https"
            || string.IsNullOrWhiteSpace(address.Host))
        {
            return false;
        }

        normalized = address.AbsoluteUri;
        return true;
    }
}
