using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Novolis.IO.GitHub;
using Novolis.Registry.Primitives;
using Novolis.Registry.Primitives.Updates;
using Novolis.Registry.Updates;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Reads an explicitly named Android APK directly from GitHub release assets
/// when a legacy release does not publish <c>Novolis.Update.json</c>.
/// </summary>
public sealed class GitHubReleaseAssetUpdateSource : IAutoUpdateSource
{
    private const long MaximumResponseBytes = 4 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly GitHubRepository _repository;
    private readonly string _assetName;
    private readonly string _userAgent;
    private readonly string? _githubToken;

    /// <summary>Creates a source for one exact release asset basename.</summary>
    public GitHubReleaseAssetUpdateSource(
        HttpClient httpClient,
        GitHubRepository repository,
        string assetName,
        string userAgent,
        string? githubToken = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        if (string.IsNullOrWhiteSpace(assetName)
            || assetName.Contains('/', StringComparison.Ordinal)
            || assetName.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The release asset name must be a basename.",
                nameof(assetName));
        }

        _assetName = assetName;
        _userAgent = string.IsNullOrWhiteSpace(userAgent)
            ? throw new ArgumentException(
                "A user-agent is required.",
                nameof(userAgent))
            : userAgent;
        _githubToken = githubToken;
    }

    /// <inheritdoc />
    public async ValueTask<UpdateSourceResult> CheckAsync(
        UpdateRequest request,
        UpdateSourceCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (request.ArtifactKind != UpdateArtifactKind.AndroidApk
            || request.Platform != UpdatePlatform.Android)
        {
            throw new UpdateSourceException(
                "target-unsupported",
                "Asset-only GitHub sources currently support Android APK requests only.");
        }
        if (!RepositoryMatches(request.Repository, _repository))
        {
            throw new UpdateSourceException(
                "repository-mismatch",
                "The update request repository does not match the configured source.");
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{Uri.EscapeDataString(_repository.Owner)}/" +
            $"{Uri.EscapeDataString(_repository.Name)}/releases?per_page=100");
        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpRequest.Headers.TryAddWithoutValidation(
            "X-GitHub-Api-Version",
            "2022-11-28");
        httpRequest.Headers.UserAgent.ParseAdd(_userAgent);
        if (!string.IsNullOrWhiteSpace(_githubToken))
        {
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _githubToken);
        }
        if (!string.IsNullOrWhiteSpace(checkpoint.ETag))
        {
            httpRequest.Headers.TryAddWithoutValidation(
                "If-None-Match",
                checkpoint.ETag);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateSourceException(
                "network-error",
                "The GitHub release asset source could not be reached.",
                exception);
        }

        using (response)
        {
            var etag = response.Headers.ETag?.Tag ?? checkpoint.ETag;
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new UpdateSourceResult
                {
                    NotModified = true,
                    ETag = etag,
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                var retryAfter = ParseRetryAfter(response);
                var code = response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "release-list-not-found",
                    HttpStatusCode.Forbidden or (HttpStatusCode)429 => "rate-limited",
                    >= HttpStatusCode.InternalServerError => "github-unavailable",
                    _ => $"release-list-http-{(int)response.StatusCode}",
                };
                var message = code == "rate-limited"
                    ? "GitHub temporarily rate-limited release asset checks."
                    : $"GitHub returned {(int)response.StatusCode} while loading releases.";
                throw new UpdateSourceException(
                    code,
                    message,
                    retryAfter: retryAfter);
            }

            var releaseJson = await ReadBoundedStringAsync(
                    response.Content,
                    cancellationToken)
                .ConfigureAwait(false);
            try
            {
                using var releases = JsonDocument.Parse(releaseJson);
                if (releases.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new UpdateSourceException(
                        "release-list-invalid",
                        "GitHub returned a release list that was not an array.");
                }

                UpdateReleaseCandidate? best = null;
                foreach (var release in releases.RootElement.EnumerateArray())
                {
                    var candidate = ReadCandidate(release, request);
                    if (candidate is null
                        || best is not null
                        && UpdateVersionComparer.Compare(
                            candidate.Version,
                            best.Version) <= 0)
                    {
                        continue;
                    }

                    best = candidate;
                }

                return new UpdateSourceResult
                {
                    Candidate = best,
                    ETag = etag,
                };
            }
            catch (JsonException exception)
            {
                throw new UpdateSourceException(
                    "release-list-invalid",
                    "GitHub returned malformed release metadata.",
                    exception);
            }
        }
    }

    private UpdateReleaseCandidate? ReadCandidate(
        JsonElement release,
        UpdateRequest request)
    {
        if (!release.TryGetProperty("draft", out var draft)
            || draft.ValueKind != JsonValueKind.False
            || !release.TryGetProperty("prerelease", out var prerelease)
            || prerelease.ValueKind != JsonValueKind.False
                && !request.IncludePrerelease
                && request.Channel == RegistryChannel.Stable)
        {
            return null;
        }
        if (!release.TryGetProperty("tag_name", out var tagElement)
            || tagElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var tag = tagElement.GetString();
        if (!UpdateVersionComparer.IsValid(tag))
            return null;
        var version = tag!.TrimStart('v');
        if (!TryGetHttpsUri(release, "html_url", out var releaseUri)
            || !TryGetDateTimeOffset(release, "published_at", out var publishedAt)
            || !release.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement)
                || !string.Equals(
                    nameElement.GetString(),
                    _assetName,
                    StringComparison.OrdinalIgnoreCase)
                || !TryGetHttpsUri(asset, "browser_download_url", out var downloadUri)
                || !TryGetSha256(asset, out var sha256)
                || !asset.TryGetProperty("size", out var sizeElement)
                || !sizeElement.TryGetInt64(out var length)
                || length <= 0)
            {
                continue;
            }

            return new UpdateReleaseCandidate
            {
                Manifest = new UpdateManifest
                {
                    AppId = request.AppId,
                    DisplayName = request.AppId,
                    DistributionMode = UpdateDistributionMode.DirectGithub,
                    Channel = request.Channel,
                    Version = version,
                    PublishedAt = publishedAt,
                    Provenance = new UpdateProvenance
                    {
                        AppId = request.AppId,
                        Repository = new Uri(
                            $"https://github.com/{_repository.Owner}/{_repository.Name}"),
                        Tag = tag,
                        ReleaseUri = releaseUri,
                    },
                },
                Artifact = new UpdateArtifact
                {
                    Name = _assetName,
                    Kind = UpdateArtifactKind.AndroidApk,
                    Target = new UpdateTarget { Platform = UpdatePlatform.Android },
                    DownloadUri = downloadUri,
                    Length = length,
                    Sha256 = sha256,
                    ContentType = asset.TryGetProperty(
                            "content_type",
                            out var contentType)
                        ? contentType.GetString()
                        : null,
                },
            };
        }

        return null;
    }

    private static async Task<string> ReadBoundedStringAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumResponseBytes)
        {
            throw new UpdateSourceException(
                "response-too-large",
                "The GitHub response exceeded the configured size limit.");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)
                       .ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaximumResponseBytes)
            {
                throw new UpdateSourceException(
                    "response-too-large",
                    "The GitHub response exceeded the configured size limit.");
            }

            output.Write(buffer, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    private static bool TryGetHttpsUri(
        JsonElement element,
        string property,
        out Uri uri)
    {
        uri = null!;
        if (!element.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String
            || !Uri.TryCreate(
                value.GetString(),
                UriKind.Absolute,
                out var parsed)
            || parsed is null
            || !string.Equals(
                parsed.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool TryGetDateTimeOffset(
        JsonElement element,
        string property,
        out DateTimeOffset value)
    {
        value = default;
        return element.TryGetProperty(property, out var propertyValue)
            && propertyValue.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                propertyValue.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
    }

    private static bool TryGetSha256(JsonElement asset, out string digest)
    {
        digest = "";
        if (!asset.TryGetProperty("digest", out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = value.GetString();
        if (raw is null
            || !raw.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hex = raw["sha256:".Length..];
        if (hex.Length != 64 || !hex.All(Uri.IsHexDigit))
            return false;

        digest = hex.ToLowerInvariant();
        return true;
    }

    private static bool RepositoryMatches(
        Uri request,
        GitHubRepository configured)
    {
        var expected = new Uri(
            $"https://github.com/{configured.Owner}/{configured.Name}");
        return Uri.Compare(
                   request,
                   expected,
                   UriComponents.SchemeAndServer | UriComponents.Path,
                   UriFormat.Unescaped,
                   StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static DateTimeOffset? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return DateTimeOffset.UtcNow + delta;
        if (response.Headers.TryGetValues(
                "X-RateLimit-Reset",
                out var values)
            && long.TryParse(
                values.FirstOrDefault(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }
}
