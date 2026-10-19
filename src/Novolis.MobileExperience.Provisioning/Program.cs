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
        "<inventory|cleanup> [options]");
    Console.Error.WriteLine(
        "  cleanup is preview-only unless --apply --yes are both supplied.");
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
