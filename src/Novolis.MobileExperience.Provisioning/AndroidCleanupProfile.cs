using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Declarative package policy for one known Android build.
/// </summary>
public sealed record AndroidCleanupProfile
{
    /// <summary>Profile schema version.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    /// <summary>Stable device profile identifier.</summary>
    [JsonPropertyName("profileId")]
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>Sanitization mode represented by this profile.</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = string.Empty;

    /// <summary>Whether an explicit apply invocation may perform destructive work.</summary>
    [JsonPropertyName("destructiveActionsEnabled")]
    public bool DestructiveActionsEnabled { get; init; }

    /// <summary>Packages that must never be changed by the profile.</summary>
    [JsonPropertyName("protectedPackages")]
    public IReadOnlyList<string> ProtectedPackages { get; init; } = [];

    /// <summary>Packages retained even though they are not system packages.</summary>
    [JsonPropertyName("retainedPackages")]
    public IReadOnlyList<string> RetainedPackages { get; init; } = [];

    /// <summary>Packages retained until compatibility testing has finished.</summary>
    [JsonPropertyName("retainUntilCompatibilityTested")]
    public IReadOnlyList<string> RetainUntilCompatibilityTested { get; init; } = [];

    /// <summary>
    /// Explicitly audited non-system packages eligible for removal.
    /// Unknown non-system packages are never removed automatically.
    /// </summary>
    [JsonPropertyName("candidateThirdPartyPackages")]
    public IReadOnlyList<string> CandidateThirdPartyPackages { get; init; } = [];

    /// <summary>Loads a profile using the repository's camel-case JSON contract.</summary>
    public static async Task<AndroidCleanupProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        await using var stream = File.OpenRead(fullPath);
        var profile = await JsonSerializer.DeserializeAsync<AndroidCleanupProfile>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return profile
            ?? throw new InvalidDataException($"Cleanup profile was empty: {fullPath}");
    }
}
