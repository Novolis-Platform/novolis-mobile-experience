using System.Text.Json;
using Novolis.IO.Mobile.Android;
using Novolis.MobileExperience.Provisioning;

var command = args.FirstOrDefault()?.ToLowerInvariant();

try
{
    return command switch
    {
        "inventory" => await RunInventoryAsync(args),
        "cleanup" => await RunCleanupAsync(args),
        "privacy" => await RunPrivacyAsync(args),
        "updates" => await RunUpdatesAsync(args),
        _ => PrintUsage(),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Provisioning failed: {ex.Message}");
    return 1;
}

static async Task<int> RunInventoryAsync(IReadOnlyList<string> arguments)
{
    var serial = ReadOption(arguments, "--serial");
    var outputDirectory = ReadOption(arguments, "--output-directory")
        ?? Path.Combine("artifacts", "inventory");
    var raw = HasFlag(arguments, "--raw");

    var adb = new AndroidDebugBridge();
    var result = await new AndroidDeviceInventoryCollector(adb).CollectAsync(serial);
    if (!result.Ok || result.Inventory is null)
    {
        Console.Error.WriteLine(
            result.Failure?.Message ?? "Android inventory collection failed.");
        return 1;
    }

    var inventory = raw
        ? result.Inventory
        : AndroidOutputRedactor.RedactInventory(result.Inventory);
    var directory = Path.GetFullPath(outputDirectory);
    Directory.CreateDirectory(directory);
    var options = JsonOptions();
    var inventoryPath = Path.Combine(directory, "inventory.json");
    await File.WriteAllTextAsync(
        inventoryPath,
        JsonSerializer.Serialize(inventory, options));

    Console.WriteLine($"Wrote {inventoryPath}");
    Console.WriteLine($"Packages: {inventory.Packages.Count}");
    Console.WriteLine($"Launcher activities: {inventory.LauncherActivities.Count}");
    Console.WriteLine($"Configured accounts: {inventory.Accounts.AccountCount}");
    foreach (var warning in result.Warnings)
        Console.WriteLine($"warning: {warning}");
    return 0;
}

static async Task<int> RunCleanupAsync(IReadOnlyList<string> arguments)
{
    var serial = ReadOption(arguments, "--serial");
    var profilePath = ReadOption(arguments, "--profile")
        ?? Path.Combine("devices", "lenovo-tab-m10-x605f", "packages.json");
    var outputDirectory = ReadOption(arguments, "--output-directory")
        ?? Path.Combine(
            "artifacts",
            "cleanup",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
    var apply = HasFlag(arguments, "--apply");
    var raw = HasFlag(arguments, "--raw");
    var json = HasFlag(arguments, "--json");

    if (apply && !HasFlag(arguments, "--yes"))
    {
        Console.Error.WriteLine(
            "Cleanup apply requires both --apply and --yes. " +
            "Without --apply this command is preview-only.");
        return 2;
    }

    var profile = await AndroidCleanupProfile.LoadAsync(profilePath);
    var adb = new AndroidDebugBridge();
    var beforeResult = await new AndroidDeviceInventoryCollector(adb).CollectAsync(serial);
    if (!beforeResult.Ok || beforeResult.Inventory is null)
    {
        Console.Error.WriteLine(
            beforeResult.Failure?.Message
            ?? "Cleanup inventory did not produce a device snapshot.");
        return 1;
    }

    var before = beforeResult.Inventory;
    var plan = AndroidCleanupPlan.Create(before, profile);
    await WriteCleanupFilesAsync(outputDirectory, before, plan, raw, "before-inventory");

    if (!apply)
    {
        var preview = new
        {
            ok = true,
            mode = "preview",
            outputDirectory = Path.GetFullPath(outputDirectory),
            removals = plan.Removals.Select(ToCleanupPayload).ToArray(),
            reviewRequired = plan.ReviewRequired
                .Select(ToCleanupPayload)
                .ToArray(),
        };
        if (json)
            Console.WriteLine(JsonSerializer.Serialize(preview, JsonOptions()));
        else
            WriteCleanupSummary(plan, outputDirectory);
        return 0;
    }

    if (!profile.DestructiveActionsEnabled)
    {
        Console.Error.WriteLine(
            "The cleanup profile does not enable destructive actions.");
        return 2;
    }

    if (before.Accounts.AccountCount != 0)
    {
        Console.Error.WriteLine(
            "Cleanup refuses to proceed while Android accounts are configured.");
        return 2;
    }

    var missingRetained = profile.RetainedPackages
        .Where(package => !before.Packages.Any(
            installed => string.Equals(
                installed.PackageName,
                package,
                StringComparison.Ordinal)))
        .ToArray();
    if (missingRetained.Length > 0)
    {
        Console.Error.WriteLine(
            "Required retained packages are missing: "
            + string.Join(", ", missingRetained));
        return 2;
    }

    if (!string.IsNullOrWhiteSpace(profile.HomeActivity))
    {
        var home = await SetAndVerifyHomeActivityAsync(
            adb,
            profile.HomeActivity,
            serial);
        if (!home.Ok)
        {
            Console.Error.WriteLine(home.Failure);
            return 1;
        }

        Console.WriteLine($"HOME activity set: {profile.HomeActivity}");
    }

    var execution = await AndroidCleanupExecutor.ExecuteAsync(
        adb,
        plan,
        outputDirectory,
        serial);
    if (!execution.Ok)
    {
        Console.Error.WriteLine(execution.Failure ?? "Cleanup failed.");
        return 1;
    }

    var afterResult = await new AndroidDeviceInventoryCollector(adb).CollectAsync(serial);
    if (!afterResult.Ok || afterResult.Inventory is null)
    {
        Console.Error.WriteLine(
            afterResult.Failure?.Message
            ?? "Cleanup completed but the after-inventory failed.");
        return 1;
    }

    var after = afterResult.Inventory;
    await WriteCleanupFilesAsync(outputDirectory, after, plan, raw, "after-inventory");
    var missingRetainedAfter = profile.RetainedPackages
        .Where(package => !after.Packages.Any(
            installed => string.Equals(
                installed.PackageName,
                package,
                StringComparison.Ordinal)))
        .ToArray();
    if (missingRetainedAfter.Length > 0)
    {
        Console.Error.WriteLine(
            "Cleanup completed, but retained packages are missing afterward: "
            + string.Join(", ", missingRetainedAfter));
        return 1;
    }

    var applied = new
    {
        ok = true,
        mode = "applied",
        outputDirectory = Path.GetFullPath(outputDirectory),
        removedPackages = execution.RemovedPackages,
        backups = execution.Backups,
        systemImageEvidence = execution.SystemImageEvidence,
        removedDeviceAdminComponents = execution.RemovedDeviceAdminComponents,
    };
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(applied, JsonOptions()));
    else
    {
        Console.WriteLine(
            $"Removed {execution.RemovedPackages.Count} audited packages.");
        Console.WriteLine($"Evidence: {Path.GetFullPath(outputDirectory)}");
        foreach (var backup in execution.Backups)
            Console.WriteLine($"Backup: {backup}");
        foreach (var evidence in execution.SystemImageEvidence)
            Console.WriteLine(
                $"System-image recovery: {evidence.PackageName} — " +
                evidence.RecoveryCommand);
    }

    return 0;
}

static async Task<int> RunPrivacyAsync(IReadOnlyList<string> arguments)
{
    var serial = ReadOption(arguments, "--serial");
    var outputDirectory = ReadOption(arguments, "--output-directory")
        ?? Path.Combine(
            "artifacts",
            "privacy",
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
    var privacyProfilePath = ReadOption(arguments, "--privacy-profile")
        ?? Path.Combine(
            "devices",
            "lenovo-tab-m10-x605f",
            "privacy.json");
    var networkProfilePath = ReadOption(arguments, "--network-profile")
        ?? Path.Combine(
            "devices",
            "lenovo-tab-m10-x605f",
            "network.json");
    var raw = HasFlag(arguments, "--raw");
    var json = HasFlag(arguments, "--json");

    using var privacyDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(privacyProfilePath));
    using var networkDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(networkProfilePath));
    var privacyRoot = privacyDocument.RootElement;
    var networkRoot = networkDocument.RootElement;
    var adb = new AndroidDebugBridge();
    var result = await new AndroidDeviceInventoryCollector(adb).CollectAsync(serial);
    if (!result.Ok || result.Inventory is null)
    {
        Console.Error.WriteLine(
            result.Failure?.Message
            ?? "Privacy inventory did not produce a device snapshot.");
        return 1;
    }

    var inventory = raw
        ? result.Inventory
        : AndroidOutputRedactor.RedactInventory(result.Inventory);
    var requiredAccountCount = ReadInt(
        privacyRoot,
        "requiredAccountCount",
        0);
    var vpnPackage = ReadObjectString(
        networkRoot,
        "vpn",
        "requiredPackage")
        ?? ReadObjectString(privacyRoot, "vpn", "packageName")
        ?? "com.nordvpn.android";
    var requiredPackages = ReadStringArray(
        privacyRoot,
        "packagePolicy",
        "requiredPackages");
    var forbiddenPackages = ReadStringArray(
        privacyRoot,
        "packagePolicy",
        "forbiddenPackages");
    var presentPackages = inventory.Packages
        .Select(package => package.PackageName)
        .ToHashSet(StringComparer.Ordinal);
    var enabledPackages = inventory.Packages
        .Where(package => package.IsEnabled)
        .Select(package => package.PackageName)
        .ToHashSet(StringComparer.Ordinal);
    var findings = new List<AndroidPrivacyFinding>();

    AddFinding(
        findings,
        "accounts",
        inventory.Accounts.AccountCount == requiredAccountCount,
        $"Expected {requiredAccountCount} configured accounts; " +
        $"observed {inventory.Accounts.AccountCount}.");
    foreach (var package in requiredPackages)
    {
        AddFinding(
            findings,
            $"required-package:{package}",
            enabledPackages.Contains(package),
            enabledPackages.Contains(package)
                ? $"Required package {package} is installed and enabled."
                : $"Required package {package} is missing or disabled.");
    }

    foreach (var package in forbiddenPackages)
    {
        AddFinding(
            findings,
            $"forbidden-package:{package}",
            !presentPackages.Contains(package),
            presentPackages.Contains(package)
                ? $"Forbidden package {package} is still visible to Android user 0."
                : $"Forbidden package {package} is absent for Android user 0.");
    }

    var nordEnabled = enabledPackages.Contains(vpnPackage);
    var vpnConfigured = string.Equals(
        inventory.Network.AlwaysOnVpnPackage,
        vpnPackage,
        StringComparison.Ordinal);
    var vpnActive = inventory.Network.ActiveNetworkIsVpn;
    AddFinding(
        findings,
        "nordvpn-enabled",
        nordEnabled,
        nordEnabled
            ? "NordVPN is installed and enabled."
            : "NordVPN is missing or disabled.");
    AddFinding(
        findings,
        "vpn-configured",
        vpnConfigured,
        vpnConfigured
            ? "NordVPN is selected as Android's always-on VPN."
            : "NordVPN is not configured as Android's always-on VPN.");
    AddFinding(
        findings,
        "vpn-active",
        vpnActive,
        vpnActive
            ? "The active default network is a VPN transport."
            : "The active default network is not a VPN transport.");

    var lockdownRequired = ReadBool(
        networkRoot,
        "vpn",
        "lockdownRecommended");
    if (lockdownRequired)
    {
        AddFinding(
            findings,
            "vpn-lockdown",
            inventory.Network.AlwaysOnVpnLockdown,
            inventory.Network.AlwaysOnVpnLockdown
                ? "Android VPN lockdown is enabled."
                : "Android VPN lockdown is not enabled.");
    }

    var failures = findings
        .Where(finding => !finding.Ok)
        .ToArray();
    var audit = new
    {
        ok = failures.Length == 0,
        collectedAtUtc = inventory.CollectedAtUtc,
        device = inventory.Device,
        accounts = inventory.Accounts,
        network = inventory.Network,
        nordVpnPackage = vpnPackage,
        findings,
        remediation = new[]
        {
            "Complete NordVPN sign-in and approve Android VPN consent on the tablet.",
            "Select NordVPN as always-on VPN and enable lockdown after the tunnel is verified.",
            "Do not treat package presence as proof that a VPN tunnel is active.",
        },
        warnings = result.Warnings,
    };
    var directory = Path.GetFullPath(outputDirectory);
    Directory.CreateDirectory(directory);
    var outputPath = Path.Combine(directory, "privacy-audit.json");
    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(audit, JsonOptions()));
    if (json)
        Console.WriteLine(JsonSerializer.Serialize(audit, JsonOptions()));
    else
    {
        Console.WriteLine($"Privacy audit: {(audit.ok ? "PASS" : "ATTENTION REQUIRED")}");
        foreach (var finding in findings)
            Console.WriteLine(
                $"  {(finding.Ok ? "PASS" : "FAIL")} " +
                $"{finding.Id}: {finding.Message}");
        Console.WriteLine($"Evidence: {outputPath}");
        foreach (var warning in result.Warnings)
            Console.WriteLine($"warning: {warning}");
    }

    return audit.ok ? 0 : 3;
}

static async Task<int> RunUpdatesAsync(IReadOnlyList<string> arguments)
{
    var action = arguments.Skip(1)
        .FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal))
        ?.ToLowerInvariant()
        ?? "check";
    if (action is not ("check" or "apply" or "maintain"))
    {
        Console.Error.WriteLine(
            $"Unknown updates action '{action}'. Use 'check', 'apply', or 'maintain'.");
        return 2;
    }

    var directGithub = HasFlag(arguments, "--direct-github");
    if (action is "apply" or "maintain"
        && (!directGithub || !HasFlag(arguments, "--yes")))
    {
        Console.Error.WriteLine(
            "Update installation requires --direct-github and --yes. " +
            "The default profile keeps device and host update access explicit.");
        return 2;
    }

    var options = new MobileUpdateOptions
    {
        CatalogDirectory = ReadOption(arguments, "--catalog-directory")
            ?? Path.Combine("catalog"),
        OutputDirectory = ReadOption(arguments, "--output-directory")
            ?? Path.Combine("artifacts", "updates"),
        Serial = ReadOption(arguments, "--serial"),
        ApplicationId = ReadOption(arguments, "--application"),
        AllowDirectGithub = directGithub,
        IncludeNotInstalled = HasFlag(arguments, "--include-not-installed")
            || action is "apply" or "maintain",
    };
    var service = new MobileUpdateService(new AndroidDebugBridge());
    var report = action switch
    {
        "apply" => await service.ApplyAsync(options),
        "maintain" => await service.MaintainAsync(options),
        _ => await service.CheckAsync(options),
    };
    var json = HasFlag(arguments, "--json");
    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions()));
    }
    else
    {
        Console.WriteLine(
            $"Updates {(report.Ok ? "completed" : "completed with attention required")}; " +
            $"source: {(report.UsedDirectGithub ? "direct GitHub on host" : "local mirror policy")}");
        foreach (var application in report.Applications)
        {
            var version = application.AvailableVersion is null
                ? application.CurrentVersion
                : $"{application.CurrentVersion} → {application.AvailableVersion}";
            Console.WriteLine(
                $"  {application.DisplayName}: {application.State} " +
                $"({version})" +
                (application.ErrorMessage is null
                    ? ""
                    : $" — {application.ErrorMessage}"));
        }

        foreach (var warning in report.Warnings)
            Console.WriteLine($"warning: {warning}");
        Console.WriteLine(
            $"Evidence: {Path.GetFullPath(Path.Combine(options.OutputDirectory, "update-report.json"))}");
    }

    return report.Ok ? 0 : 3;
}

static void AddFinding(
    ICollection<AndroidPrivacyFinding> findings,
    string id,
    bool ok,
    string message) =>
    findings.Add(new AndroidPrivacyFinding
    {
        Id = id,
        Ok = ok,
        Message = message,
    });

static int ReadInt(
    JsonElement root,
    string property,
    int fallback)
{
    return root.TryGetProperty(property, out var value)
        && value.TryGetInt32(out var parsed)
        ? parsed
        : fallback;
}

static bool ReadBool(
    JsonElement root,
    string objectProperty,
    string property)
{
    return root.TryGetProperty(objectProperty, out var nested)
        && nested.ValueKind == JsonValueKind.Object
        && nested.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.True;
}

static string? ReadObjectString(
    JsonElement root,
    string objectProperty,
    string property)
{
    if (!root.TryGetProperty(objectProperty, out var nested)
        || nested.ValueKind != JsonValueKind.Object
        || !nested.TryGetProperty(property, out var value)
        || value.ValueKind != JsonValueKind.String)
    {
        return null;
    }

    return value.GetString();
}

static IReadOnlyList<string> ReadStringArray(
    JsonElement root,
    string objectProperty,
    string property)
{
    if (!root.TryGetProperty(objectProperty, out var nested)
        || nested.ValueKind != JsonValueKind.Object
        || !nested.TryGetProperty(property, out var value)
        || value.ValueKind != JsonValueKind.Array)
    {
        return [];
    }

    return value.EnumerateArray()
        .Where(element => element.ValueKind == JsonValueKind.String)
        .Select(element => element.GetString())
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Cast<string>()
        .ToArray();
}

static async Task<(bool Ok, string? Failure)> SetAndVerifyHomeActivityAsync(
    AndroidDebugBridge adb,
    string component,
    string? serial)
{
    if (component.Any(char.IsWhiteSpace)
        || component.Count(character => character == '/') != 1)
    {
        return (false, $"Invalid HOME activity component in profile: {component}");
    }

    var set = await adb.ShellAsync(
            $"cmd package set-home-activity {component}",
            serial)
        .ConfigureAwait(false);
    if (!set.Ok)
    {
        return (
            false,
            $"Could not set HOME activity to {component}: {set.Diagnostic}");
    }

    var resolved = await adb.ShellAsync(
            "cmd package resolve-activity --brief " +
            "-a android.intent.action.MAIN -c android.intent.category.HOME",
            serial)
        .ConfigureAwait(false);
    if (!resolved.Ok
        || !resolved.StdOut.Contains(component, StringComparison.Ordinal))
    {
        return (
            false,
            $"HOME activity verification failed. Expected {component}; " +
            $"resolver returned: {resolved.Diagnostic}");
    }

    return (true, null);
}

static async Task WriteCleanupFilesAsync(
    string outputDirectory,
    AndroidDeviceInventory inventory,
    AndroidCleanupPlan plan,
    bool raw,
    string inventoryFileName)
{
    var directory = Path.GetFullPath(outputDirectory);
    Directory.CreateDirectory(directory);
    var options = JsonOptions();
    var outputInventory = raw
        ? inventory
        : AndroidOutputRedactor.RedactInventory(inventory);
    await File.WriteAllTextAsync(
        Path.Combine(directory, $"{inventoryFileName}.json"),
        JsonSerializer.Serialize(outputInventory, options));
    await File.WriteAllTextAsync(
        Path.Combine(directory, "plan.json"),
        JsonSerializer.Serialize(
            new
            {
                profile = plan.Profile,
                decisions = plan.Decisions,
                removals = plan.Removals,
                reviewRequired = plan.ReviewRequired,
            },
            options));
}

static object ToCleanupPayload(AndroidCleanupDecision decision) =>
    new
    {
        package = decision.Package.PackageName,
        system = decision.Package.IsSystem,
        enabled = decision.Package.IsEnabled,
        remove = decision.Remove,
        operation = decision.Operation,
        reason = decision.Reason,
    };

static void WriteCleanupSummary(
    AndroidCleanupPlan plan,
    string outputDirectory)
{
    Console.WriteLine($"Cleanup preview: {Path.GetFullPath(outputDirectory)}");
    Console.WriteLine($"Removals: {plan.Removals.Count}");
    foreach (var removal in plan.Removals)
        Console.WriteLine(
            $"  REMOVE ({removal.Operation}) {removal.Package.PackageName} — {removal.Reason}");
    Console.WriteLine($"Review required: {plan.ReviewRequired.Count}");
    foreach (var review in plan.ReviewRequired)
        Console.WriteLine($"  KEEP/REVIEW {review.Package.PackageName} — {review.Reason}");
    Console.WriteLine(
        "Only explicitly audited candidates are included; all other system packages are retained.");
}

static JsonSerializerOptions JsonOptions() =>
    new() { WriteIndented = true };

static int PrintUsage()
{
    Console.Error.WriteLine(
        "Usage: Novolis.MobileExperience.Provisioning " +
        "<inventory|cleanup|privacy|updates> [options]");
    Console.Error.WriteLine(
        "  cleanup is preview-only unless --apply --yes are both supplied.");
    Console.Error.WriteLine(
        "  privacy is read-only and returns exit code 3 until the VPN profile is satisfied.");
    Console.Error.WriteLine(
        "  updates check is host-only and local-mirror by default; " +
        "use updates check --direct-github for explicit public-release access.");
    Console.Error.WriteLine(
        "  updates maintain installs and refreshes every automatic release-backed app; " +
        "use --direct-github --yes.");
    return 2;
}

static bool HasFlag(IReadOnlyList<string> arguments, string name) =>
    arguments.Any(argument =>
        string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

static string? ReadOption(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            return arguments[index + 1];
    }

    return null;
}
