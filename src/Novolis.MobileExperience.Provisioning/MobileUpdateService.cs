using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Novolis.IO.GitHub;
using Novolis.IO.Mobile.Android;
using Novolis.Registry.GitHub;
using Novolis.Registry.Primitives;
using Novolis.Registry.Primitives.Updates;
using Novolis.Registry.Updates;

namespace Novolis.MobileExperience.Provisioning;

/// <summary>
/// Runs the NME host-side update workflow. Network access is explicit and
/// remains on the provisioning host; the Android shell is never used as an
/// update transport.
/// </summary>
public sealed class MobileUpdateService
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions EvidenceJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly AndroidDebugBridge _adb;

    /// <summary>Creates an update service bound to one host ADB bridge.</summary>
    public MobileUpdateService(AndroidDebugBridge adb)
    {
        _adb = adb ?? throw new ArgumentNullException(nameof(adb));
    }

    /// <summary>Checks approved applications without installing anything.</summary>
    public async Task<MobileUpdateReport> CheckAsync(
        MobileUpdateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var run = await CheckCoreAsync(options, cancellationToken).ConfigureAwait(false);
        await WriteReportAsync(options.OutputDirectory, run.Report, cancellationToken)
            .ConfigureAwait(false);
        return run.Report;
    }

    /// <summary>
    /// Checks, downloads, verifies, and installs one explicitly selected
    /// trusted application. The caller remains responsible for confirmation.
    /// </summary>
    public async Task<MobileUpdateReport> ApplyAsync(
        MobileUpdateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ApplicationId))
            throw new ArgumentException(
                "Apply requires an application id.",
                nameof(options));
        if (!options.AllowDirectGithub)
            throw new InvalidOperationException(
                "Apply requires explicit host GitHub access. " +
                "The default local-mirror policy did not permit network access.");

        var run = await CheckCoreAsync(
                options with { IncludeNotInstalled = true },
                cancellationToken)
            .ConfigureAwait(false);
        var selection = run.Selections.SingleOrDefault();
        if (selection is null)
        {
            var failed = run.Report with
            {
                Ok = false,
                Applications =
                [
                    new MobileUpdateApplicationReport
                    {
                        ApplicationId = options.ApplicationId,
                        DisplayName = options.ApplicationId,
                        PackageName = "",
                        CurrentVersion = "unknown",
                        State = "not-found",
                        ErrorCode = "application-not-found",
                        ErrorMessage =
                            $"No approved application matched '{options.ApplicationId}'.",
                    },
                ],
            };
            await WriteReportAsync(options.OutputDirectory, failed, cancellationToken)
                .ConfigureAwait(false);
            return failed;
        }

        var report = run.Report;
        var current = report.Applications
            .Single(application => application.ApplicationId == selection.Application.Id);
        var updated = await InstallSelectionAsync(
                selection,
                current,
                options,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReplaceApplicationReportAsync(
                report,
                updated,
                options,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks and installs every release-backed application marked for automatic
    /// maintenance. GitHub access and confirmation remain explicit host actions.
    /// </summary>
    public async Task<MobileUpdateReport> MaintainAsync(
        MobileUpdateOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.AllowDirectGithub)
            throw new InvalidOperationException(
                "Automatic maintenance requires explicit host GitHub access. " +
                "The default local-mirror policy did not permit network access.");

        var run = await CheckCoreAsync(
                options with
                {
                    IncludeNotInstalled = true,
                    AutomaticOnly = true,
                },
                cancellationToken)
            .ConfigureAwait(false);
        var report = run.Report;

        foreach (var selection in run.Selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = report.Applications
                .Single(application =>
                    application.ApplicationId == selection.Application.Id);
            var updated = await InstallSelectionAsync(
                    selection,
                    current,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
            report = report with
            {
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Ok = updated.State is "installed" or "up-to-date"
                    && report.Applications
                        .Where(application =>
                            application.ApplicationId != updated.ApplicationId)
                        .All(IsSuccessfulState),
                Applications = report.Applications
                    .Select(application =>
                        application.ApplicationId == updated.ApplicationId
                            ? updated
                            : application)
                    .ToArray(),
            };
            await WriteReportAsync(
                    options.OutputDirectory,
                    report,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        report = report with
        {
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Ok = report.Applications.All(IsSuccessfulState),
        };
        await WriteReportAsync(options.OutputDirectory, report, cancellationToken)
            .ConfigureAwait(false);
        return report;
    }

    private async Task<MobileUpdateApplicationReport> InstallSelectionAsync(
        Selection selection,
        MobileUpdateApplicationReport current,
        MobileUpdateOptions options,
        CancellationToken cancellationToken)
    {
        if (selection.Candidate is null)
            return current;
        if (!current.TrustedForInstall)
        {
            return current with
            {
                State = "blocked-untrusted-signing-identity",
                ErrorCode = "signing-identity-not-pinned",
                ErrorMessage =
                    "The trust catalog has no approved signing certificate for this application.",
            };
        }

        var downloadDirectory = Path.Combine(
            Path.GetFullPath(options.OutputDirectory),
            "downloads");
        Directory.CreateDirectory(downloadDirectory);
        using var httpClient = CreateGithubHttpClient(
            TimeSpan.FromMinutes(5),
            selection.GitHubToken);
        var downloader = new FileArtifactDownloader(
            httpClient,
            new FileArtifactDownloaderOptions
            {
                DestinationDirectory = downloadDirectory,
                MaximumBytes = 2L * 1024 * 1024 * 1024,
            });

        DownloadedUpdateArtifact downloaded;
        try
        {
            downloaded = await downloader.DownloadAsync(
                    selection.Candidate,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return current with
            {
                State = "download-failed",
                ErrorCode = "download-failed",
                ErrorMessage = exception.Message,
            };
        }

        var inspection = await AndroidApkInspector.InspectAsync(
                downloaded.Path,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var trustDigests = selection.Trust.SigningCertificateSha256
            .Select(AndroidApkInspector.NormalizeCertificateDigest)
            .Where(digest => digest is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var deviceAbis = selection.DeviceAbiList
            .Concat([selection.DeviceAbi])
            .Where(abi => !string.IsNullOrWhiteSpace(abi))
            .Select(NormalizeAbi)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var apkAbis = inspection.NativeAbis
            .Select(NormalizeAbi)
            .ToArray();
        var abiCompatible = apkAbis.Length == 0
            || apkAbis.Any(deviceAbis.Contains);
        var packageMatches = string.Equals(
            inspection.PackageName,
            selection.Application.PackageName,
            StringComparison.Ordinal);
        var signingMatches = inspection.SigningCertificateSha256 is not null
            && trustDigests.Contains(inspection.SigningCertificateSha256);
        if (!inspection.Ok || !packageMatches || !abiCompatible || !signingMatches)
        {
            var reason = !inspection.Ok
                ? "APK metadata or signing inspection failed."
                : !packageMatches
                    ? $"APK package '{inspection.PackageName ?? "unknown"}' did not match " +
                      $"'{selection.Application.PackageName}'."
                    : !abiCompatible
                        ? "APK native ABI did not match the device ABI."
                        : "APK signing certificate was not pinned in the trust catalog.";
            return current with
            {
                State = "blocked-apk-verification",
                DownloadPath = downloaded.Path,
                ErrorCode = "apk-verification-failed",
                ErrorMessage = reason,
            };
        }

        var install = await new AndroidAppInstaller(_adb).InstallAsync(
                downloaded.Path,
                new ApkInstallOptions
                {
                    Serial = options.Serial,
                    Reinstall = true,
                    AllowDowngrade = false,
                    ExpectedPackageName = selection.Application.PackageName,
                    VerifyInstalled = true,
                    DeviceWaitTimeout = TimeSpan.FromSeconds(45),
                },
                cancellationToken)
            .ConfigureAwait(false);
        return current with
        {
            State = install.Ok ? "installed" : "install-failed",
            DownloadPath = downloaded.Path,
            ErrorCode = install.Ok ? null : "install-failed",
            ErrorMessage = install.Ok ? null : install.Message,
        };
    }

    private static bool IsSuccessfulState(MobileUpdateApplicationReport report) =>
        report.State is "installed" or "up-to-date";

    private async Task<(MobileUpdateReport Report, IReadOnlyList<Selection> Selections)> CheckCoreAsync(
        MobileUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var catalogDirectory = Path.GetFullPath(options.CatalogDirectory);
        var catalog = await LoadAsync<CatalogDocument>(
                Path.Combine(catalogDirectory, "applications.json"),
                cancellationToken)
            .ConfigureAwait(false);
        var sources = await LoadAsync<SourceDocument>(
                Path.Combine(catalogDirectory, "sources.json"),
                cancellationToken)
            .ConfigureAwait(false);
        var trust = await LoadAsync<TrustDocument>(
                Path.Combine(catalogDirectory, "trust.json"),
                cancellationToken)
            .ConfigureAwait(false);
        var applications = catalog.Applications
            .Where(application =>
                (!options.AutomaticOnly
                    || string.Equals(
                        application.Update,
                        "automatic",
                        StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(options.ApplicationId)
                    || string.Equals(
                        application.Id,
                        options.ApplicationId,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        application.UpdateAppId,
                        options.ApplicationId,
                        StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var warnings = new List<string>();
        var reports = new List<MobileUpdateApplicationReport>();
        var selections = new List<Selection>();
        var usedDirectGithub = options.AllowDirectGithub;
        var requiresGitHubCliAuth = options.AllowDirectGithub
            && sources.Sources.Any(source =>
                source.Enabled
                && string.Equals(
                    source.Authentication,
                    "GitHubCli",
                    StringComparison.OrdinalIgnoreCase));
        var githubToken = requiresGitHubCliAuth
            ? await GitHubAuthentication.TryGetTokenAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
        var device = await _adb.GetDeviceInfoAsync(options.Serial, cancellationToken)
            .ConfigureAwait(false);
        using var httpClient = CreateGithubHttpClient(TimeSpan.FromSeconds(45));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Novolis-Mobile-Experience");

        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trustEntry = trust.TrustedApplications.FirstOrDefault(entry =>
                string.Equals(entry.Id, application.Id, StringComparison.Ordinal)
                && string.Equals(entry.PackageName, application.PackageName, StringComparison.Ordinal));
            var trusted = trustEntry is
            {
                Status: "approved",
                SigningCertificateSha256.Count: > 0,
            };
            var installed = await _adb.TryGetPackageInfoAsync(
                    application.PackageName,
                    options.Serial,
                    cancellationToken)
                .ConfigureAwait(false);
            var installedVersion = installed?.VersionName;
            var hasInstalled = installed?.IsInstalled == true;
            if (!hasInstalled && !options.IncludeNotInstalled)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "not-installed",
                    trusted,
                    trustEntry));
                continue;
            }

            var source = sources.Sources.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, application.Source, StringComparison.Ordinal));
            if (source is null)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "configuration-error",
                    trusted,
                    trustEntry,
                    "source-not-found",
                    $"Source '{application.Source}' is not defined."));
                continue;
            }

            if (!source.Enabled)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "source-disabled",
                    trusted,
                    trustEntry));
                continue;
            }

            if (!options.AllowDirectGithub)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "local-mirror-required",
                    trusted,
                    trustEntry,
                    "direct-github-disabled",
                    "The device profile keeps direct GitHub access disabled. " +
                    "Configure a local mirror or pass --direct-github explicitly."));
                continue;
            }

            var usesGitHubCliAuth = string.Equals(
                source.Authentication,
                "GitHubCli",
                StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(
                    source.Authentication,
                    "None",
                    StringComparison.OrdinalIgnoreCase)
                && !usesGitHubCliAuth)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "configuration-error",
                    trusted,
                    trustEntry,
                    "authentication-unsupported",
                    $"Unsupported GitHub source authentication '{source.Authentication}'."));
                continue;
            }
            if (usesGitHubCliAuth && githubToken is null)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "check-failed",
                    trusted,
                    trustEntry,
                    "github-authentication-required",
                    "This private GitHub release source requires an authenticated " +
                    "GitHub CLI session or GH_TOKEN/GITHUB_TOKEN on the host."));
                continue;
            }

            var usesReleaseManifest = string.Equals(
                source.Kind,
                "GitHubReleases",
                StringComparison.OrdinalIgnoreCase);
            var usesReleaseAsset = string.Equals(
                source.Kind,
                "GitHubReleaseAssets",
                StringComparison.OrdinalIgnoreCase);
            if (!usesReleaseManifest && !usesReleaseAsset)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "configuration-error",
                    trusted,
                    trustEntry,
                    "source-kind-unsupported",
                    $"Unsupported update source kind '{source.Kind}'."));
                continue;
            }
            if (usesReleaseAsset
                && string.IsNullOrWhiteSpace(application.ReleaseAssetName))
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "configuration-error",
                    trusted,
                    trustEntry,
                    "asset-name-missing",
                    "An asset-only GitHub source requires releaseAssetName."));
                continue;
            }

            var repository = ParseRepository(source.Repository);
            if (repository is null
                || !Enum.TryParse<RegistryChannel>(
                    source.Channel,
                    ignoreCase: true,
                    out var channel))
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    "configuration-error",
                    trusted,
                    trustEntry,
                    "source-invalid",
                    "The source repository or channel is invalid."));
                continue;
            }

            var currentVersion = NormalizeVersion(installedVersion);
            var requestedChannel = string.IsNullOrWhiteSpace(application.Channel)
                ? source.Channel
                : application.Channel;
            var request = new UpdateRequest
            {
                AppId = application.UpdateAppId,
                Repository = new Uri($"https://github.com/{repository.Owner}/{repository.Name}"),
                Channel = Enum.TryParse<RegistryChannel>(
                    requestedChannel,
                    ignoreCase: true,
                    out var applicationChannel)
                    ? applicationChannel
                    : channel,
                CurrentVersion = currentVersion,
                Platform = UpdatePlatform.Android,
                ArtifactKind = UpdateArtifactKind.AndroidApk,
            };
            var statePath = Path.Combine(
                Path.GetFullPath(options.OutputDirectory),
                "state",
                SafeFileName(application.Id) + ".json");
            IAutoUpdateSource sourceClient = usesReleaseAsset
                ? new GitHubReleaseAssetUpdateSource(
                    httpClient,
                    repository,
                    application.ReleaseAssetName!,
                    "Novolis-Mobile-Experience",
                    githubToken)
                : new GitHubUpdateSource(
                    httpClient,
                    new GitHubUpdateSourceOptions
                    {
                        Repository = repository,
                        UserAgent = "Novolis-Mobile-Experience",
                    });
            await using var coordinator = new UpdateCoordinator(
                request,
                sourceClient,
                new JsonUpdateStateStore(statePath),
                new FileArtifactDownloader(
                    httpClient,
                    new FileArtifactDownloaderOptions
                    {
                        DestinationDirectory = Path.Combine(
                            Path.GetFullPath(options.OutputDirectory),
                            "downloads"),
                    }));
            var snapshot = await coordinator.CheckAsync(
                    force: true,
                    cancellationToken)
                .ConfigureAwait(false);
            if (snapshot.Candidate is null)
            {
                reports.Add(CreateReport(
                    application,
                    installedVersion,
                    snapshot.ErrorCode is null ? "up-to-date" : "check-failed",
                    trusted,
                    trustEntry,
                    snapshot.ErrorCode,
                    snapshot.ErrorMessage));
                continue;
            }

            var candidate = snapshot.Candidate;
            var report = CreateReport(
                application,
                installedVersion,
                trusted ? "available" : "available-untrusted",
                trusted,
                trustEntry,
                candidate: candidate);
            reports.Add(report);
            selections.Add(
                new Selection(
                    application,
                    trustEntry ?? new TrustEntry(),
                    candidate,
                    device.Abi ?? "",
                    (device.AbiList ?? "")
                        .Split(
                            [',', ' ', ';'],
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    githubToken));
        }

        if (applications.Length == 0)
            warnings.Add(
                string.IsNullOrWhiteSpace(options.ApplicationId)
                    ? "The application catalog contains no approved applications."
                    : $"No approved application matched '{options.ApplicationId}'.");
        var reportResult = new MobileUpdateReport
        {
            StartedAtUtc = started,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            UsedDirectGithub = usedDirectGithub,
            Ok = reports.All(report =>
                report.State is not "check-failed" and not "configuration-error"),
            Applications = reports,
            Warnings = warnings,
        };
        return (reportResult, selections);
    }

    private static MobileUpdateApplicationReport CreateReport(
        ApplicationEntry application,
        string? installedVersion,
        string state,
        bool trusted,
        TrustEntry? trust,
        string? errorCode = null,
        string? errorMessage = null,
        UpdateReleaseCandidate? candidate = null) =>
        new()
        {
            ApplicationId = application.Id,
            DisplayName = application.DisplayName,
            PackageName = application.PackageName,
            CurrentVersion = installedVersion ?? "not-installed",
            State = state,
            AvailableVersion = candidate?.Version,
            ArtifactName = candidate?.Artifact.Name,
            ArtifactSha256 = candidate?.Artifact.Sha256,
            ReleaseUri = candidate?.ReleaseUri,
            TrustedForInstall = trusted,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };

    private static async Task<MobileUpdateReport> ReplaceApplicationReportAsync(
        MobileUpdateReport report,
        MobileUpdateApplicationReport application,
        MobileUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var replaced = report with
        {
            Ok = application.State is "installed" or "available" or "up-to-date",
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Applications = report.Applications
                .Select(existing =>
                    existing.ApplicationId == application.ApplicationId
                        ? application
                        : existing)
                .ToArray(),
        };
        await WriteReportAsync(options.OutputDirectory, replaced, cancellationToken)
            .ConfigureAwait(false);
        return replaced;
    }

    private static async Task WriteReportAsync(
        string outputDirectory,
        MobileUpdateReport report,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
                Path.Combine(directory, "update-report.json"),
                JsonSerializer.Serialize(report, EvidenceJsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<T> LoadAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(
                stream,
                CatalogJsonOptions,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException($"Catalog file '{path}' was empty.");
    }

    private static GitHubRepository? ParseRepository(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim().TrimEnd('/');
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            normalized = uri.AbsolutePath.Trim('/');
        var parts = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? new GitHubRepository { Owner = parts[0], Name = parts[1] }
            : null;
    }

    private static string NormalizeVersion(string? version) =>
        UpdateVersionComparer.IsValid(version)
            ? version!
            : "2000.1.0";

    private static string NormalizeAbi(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "arm64" or "aarch64" => "arm64-v8a",
            "arm" or "armeabi" => "armeabi-v7a",
            "x86_64" => "x86-64",
            _ => value.Trim().ToLowerInvariant(),
        };

    private static string SafeFileName(string value) =>
        new(value.Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? character
                    : '_')
            .ToArray());

    private static HttpClient CreateGithubHttpClient(
        TimeSpan timeout,
        string? githubToken = null)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ConnectIpv4Async,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler)
        {
            Timeout = timeout,
        };
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", githubToken);
        }

        return client;
    }

    private static async ValueTask<Stream> ConnectIpv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(
                context.DnsEndPoint.Host,
                cancellationToken)
            .ConfigureAwait(false);
        Exception? last = null;
        foreach (var address in addresses.Where(
                     candidate => candidate.AddressFamily == AddressFamily.InterNetwork))
        {
            var socket = new Socket(
                address.AddressFamily,
                SocketType.Stream,
                ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(
                        address,
                        context.DnsEndPoint.Port,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException
                or OperationCanceledException)
            {
                last = exception;
                socket.Dispose();
            }
        }

        throw new HttpRequestException(
            $"Could not connect to {context.DnsEndPoint.Host}.",
            last);
    }

    private sealed record Selection(
        ApplicationEntry Application,
        TrustEntry Trust,
        UpdateReleaseCandidate Candidate,
        string DeviceAbi,
        IReadOnlyList<string> DeviceAbiList,
        string? GitHubToken);

    private sealed class CatalogDocument
    {
        public List<ApplicationEntry> Applications { get; init; } = [];
    }

    private sealed class SourceDocument
    {
        public List<SourceEntry> Sources { get; init; } = [];
    }

    private sealed class TrustDocument
    {
        public List<TrustEntry> TrustedApplications { get; init; } = [];
    }

    private sealed class SourceEntry
    {
        public string Id { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Repository { get; init; } = "";
        public string Channel { get; init; } = "stable";
        public bool Enabled { get; init; }
        public string Authentication { get; init; } = "None";
    }

    private sealed class ApplicationEntry
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string PackageName { get; init; } = "";
        public string Source { get; init; } = "";
        public string UpdateAppId { get; init; } = "";
        public string? ReleaseAssetName { get; init; }
        public string Channel { get; init; } = "";
        public string Update { get; init; } = "";
    }

    private sealed class TrustEntry
    {
        public string Id { get; init; } = "";
        public string PackageName { get; init; } = "";
        public string Status { get; init; } = "";
        public List<string> SigningCertificateSha256 { get; init; } = [];
    }
}
