[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$Serial = "HA1417X9",
    [string]$TaskName = "Novolis-Mobile-Experience-Maintenance",
    [string]$At = "03:00",
    [switch]$Unregister,
    [switch]$RunMaintenance
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)

$project = Join-Path `
    $RepositoryRoot `
    "src\Novolis.MobileExperience.Provisioning\Novolis.MobileExperience.Provisioning.csproj"
$catalog = Join-Path $RepositoryRoot "catalog"
$updatesRoot = Join-Path $RepositoryRoot "artifacts\updates\scheduled"
$pwsh = (Get-Command pwsh.exe -ErrorAction Stop).Source
$dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source

if ($Unregister) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Output "Removed scheduled task '$TaskName'."
    exit 0
}

if ($RunMaintenance) {
    $runDirectory = Join-Path `
        $updatesRoot `
        (Get-Date -Format "yyyyMMdd-HHmmss")
    New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
    & $dotnet `
        "run" `
        "--project" $project `
        "-p:NovolisUseProjectReferences=true" `
        "--" `
        "updates" `
        "maintain" `
        "--direct-github" `
        "--serial" $Serial `
        "--catalog-directory" $catalog `
        "--output-directory" $runDirectory `
        "--yes" *>&1 |
        Tee-Object -FilePath (Join-Path $runDirectory "scheduled-maintenance.log")
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "Provisioning project was not found: $project"
}

$timeOfDay = [TimeSpan]::Parse($At)
$taskScript = $MyInvocation.MyCommand.Path
$taskArguments = @(
    "-NoLogo"
    "-NoProfile"
    "-NonInteractive"
    "-WindowStyle"
    "Hidden"
    "-File"
    "`"$taskScript`""
    "-RunMaintenance"
    "-RepositoryRoot"
    "`"$RepositoryRoot`""
    "-Serial"
    "`"$Serial`""
) -join " "

$action = New-ScheduledTaskAction `
    -Execute $pwsh `
    -Argument $taskArguments `
    -WorkingDirectory $RepositoryRoot
$trigger = New-ScheduledTaskTrigger `
    -Daily `
    -At ([DateTime]::Today.Add($timeOfDay))
$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal `
    -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Host-side maintenance for approved Novolis Android release assets." `
    -Force | Out-Null

Write-Output "Registered daily task '$TaskName' at $At for Android serial $Serial."
