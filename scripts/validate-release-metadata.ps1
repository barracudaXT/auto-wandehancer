param(
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$assemblyInfoPath = Join-Path $repoRoot 'BuildVersion.cs'
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'

function Normalize-Version {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'Version value cannot be empty.'
    }

    # AssemblyVersion holds four numbers only, so a pre-release tag such as
    # 1.1.0.0-rc.1 must compare and look up its notes as 1.1.0.0.
    return ($Value.Trim().TrimStart('v', 'V') -replace '-.*$', '')
}

function Get-ChangelogSection {
    param(
        [string]$Content,
        [string]$TargetVersion
    )

    $normalizedTargetVersion = Normalize-Version $TargetVersion
    $normalizedContent = $Content -replace "`r`n", "`n" -replace "`r", "`n"
    $lines = $normalizedContent -split "`n"
    $builder = New-Object System.Text.StringBuilder
    $isInsideSection = $false

    foreach ($line in $lines) {
        $match = [regex]::Match($line, '^##\s+\[(?<version>[^\]]+)\]')
        if ($match.Success) {
            if ($isInsideSection) {
                break
            }

            $isInsideSection = (Normalize-Version $match.Groups['version'].Value) -eq $normalizedTargetVersion
            continue
        }

        if (-not $isInsideSection) {
            continue
        }

        [void]$builder.AppendLine($line)
    }

    return $builder.ToString().Trim()
}

if (-not (Test-Path $assemblyInfoPath)) {
    throw "BuildVersion.cs not found: $assemblyInfoPath"
}

if (-not (Test-Path $changelogPath)) {
    throw "CHANGELOG.md not found: $changelogPath"
}

$assemblyInfoContent = Get-Content -Path $assemblyInfoPath -Raw
$changelogContent = Get-Content -Path $changelogPath -Raw

$assemblyVersionMatch = [regex]::Match($assemblyInfoContent, '(?m)^\s*\[assembly:\s*AssemblyVersion\("(?<version>[^"]+)"\)\]')
$fileVersionMatch = [regex]::Match($assemblyInfoContent, '(?m)^\s*\[assembly:\s*AssemblyFileVersion\("(?<version>[^"]+)"\)\]')

if (-not $assemblyVersionMatch.Success) {
    throw 'AssemblyVersion was not found in BuildVersion.cs.'
}

if (-not $fileVersionMatch.Success) {
    throw 'AssemblyFileVersion was not found in BuildVersion.cs.'
}

$assemblyVersion = Normalize-Version $assemblyVersionMatch.Groups['version'].Value
$fileVersion = Normalize-Version $fileVersionMatch.Groups['version'].Value

if ($assemblyVersion -ne $fileVersion) {
    throw "AssemblyVersion ($assemblyVersion) and AssemblyFileVersion ($fileVersion) must match."
}

$changelogVersionMatches = [regex]::Matches($changelogContent, '(?m)^##\s+\[(?<version>[^\]]+)\]')
if ($changelogVersionMatches.Count -eq 0) {
    throw 'CHANGELOG.md must contain at least one version section.'
}

$latestChangelogVersion = Normalize-Version $changelogVersionMatches[0].Groups['version'].Value
if ($latestChangelogVersion -ne $assemblyVersion) {
    throw "The first CHANGELOG.md section ($latestChangelogVersion) must match BuildVersion.cs version ($assemblyVersion)."
}

$latestSection = Get-ChangelogSection -Content $changelogContent -TargetVersion $assemblyVersion
if ([string]::IsNullOrWhiteSpace($latestSection)) {
    throw "CHANGELOG.md section '$assemblyVersion' is empty."
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $normalizedExpectedVersion = Normalize-Version $ExpectedVersion
    if ($normalizedExpectedVersion -ne $assemblyVersion) {
        throw "Release tag version ($normalizedExpectedVersion) must match BuildVersion.cs version ($assemblyVersion)."
    }

    $expectedSection = Get-ChangelogSection -Content $changelogContent -TargetVersion $normalizedExpectedVersion
    if ([string]::IsNullOrWhiteSpace($expectedSection)) {
        throw "CHANGELOG.md section '$normalizedExpectedVersion' is missing or empty."
    }
}

# The version source is only real if both product projects compile it, and only
# single if neither AssemblyInfo still declares its own. A missing link leaves an
# assembly at 0.0.0.0; a surviving attribute is a CS0579 build failure or, worse,
# a second source of truth. Neither is detectable from the text alone.
#
# Match the XML element itself, not the raw text: a commented-out or
# documentation sample containing the same string still matches a plain
# substring test, and that bypass was reproduced (validator passed while the
# watcher silently built as 0.0.0.0).
foreach ($project in @('WandEnhancer\WandEnhancer.csproj', 'WandEnhancer.AutoPatch\WandEnhancer.AutoPatch.csproj')) {
    $projectPath = Join-Path $repoRoot $project
    if (-not (Test-Path $projectPath)) { throw "Project not found: $projectPath" }
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($projectPath)
    $compiles = $xml.SelectNodes('//*[local-name()="Compile"]')
    $linked = @($compiles | Where-Object {
        $_.GetAttribute('Include').Replace('\', '/') -eq '../BuildVersion.cs'
    })
    if ($linked.Count -eq 0) {
        throw "$project does not compile ..\BuildVersion.cs, so that assembly would ship version 0.0.0.0."
    }
}

foreach ($info in @('WandEnhancer\Properties\AssemblyInfo.cs', 'WandEnhancer.AutoPatch\Properties\AssemblyInfo.cs')) {
    $infoPath = Join-Path $repoRoot $info
    if (-not (Test-Path $infoPath)) { throw "AssemblyInfo not found: $infoPath" }
    $infoContent = Get-Content -Path $infoPath -Raw
    if ($infoContent -match '(?m)^\s*\[assembly:\s*Assembly(File)?Version\(') {
        throw "$info still declares a version attribute; the product version has two sources."
    }
}

# The installer fallback is a packaged literal. Updating it once does not stop it
# drifting again, so require it to equal the shared version. CI passes
# /DMyAppVersion and overrides it, but a hand-run iscc must not stamp a stale value.
$installerPath = Join-Path $repoRoot 'installer\WandEnhancer.iss'
if (-not (Test-Path $installerPath)) { throw "Installer script not found: $installerPath" }
$installerContent = Get-Content -Path $installerPath -Raw
$fallbackMatch = [regex]::Match($installerContent, '(?m)^\s*#define\s+MyAppVersion\s+"(?<version>[^"]+)"')
if (-not $fallbackMatch.Success) {
    throw 'installer/WandEnhancer.iss has no MyAppVersion fallback to validate.'
}
$fallbackVersion = Normalize-Version $fallbackMatch.Groups['version'].Value
if ($fallbackVersion -ne $assemblyVersion) {
    throw "Installer fallback ($fallbackVersion) must match the shared product version ($assemblyVersion)."
}

Write-Host "Validated release metadata for version $assemblyVersion"

# The watcher and the installer's enable step must stay wired to each other.
# Two shipped releases failed silently here: the watcher died at assembly load
# because the installer never deployed the WandEnhancer assembly it binds to, and
# a silent install skips [Run] entirely unless MERGETASKS names the task -- so the
# updater's own arguments left auto-patch permanently disabled after an update.
if (Select-String -Path 'WandEnhancer.AutoPatch/WandEnhancer.AutoPatch.csproj' `
        -SimpleMatch 'WandEnhancer\WandEnhancer.csproj' -Quiet) {
    # The watcher binds WandEnhancer at load time; the installer must ship that file
    # into the same directory or the process dies before it can log anything.
    if (-not (Select-String -Path $installerPath -SimpleMatch 'WandEnhancer.exe"; DestDir: "{app}\AutoPatch"' -Quiet)) {
        throw 'WandEnhancer.AutoPatch references the app project, so the installer must deploy WandEnhancer.exe into {app}\AutoPatch.'
    }

    $updaterArgs = Select-String -Path 'WandEnhancer.AutoPatch/UpdateInstaller.cs' -Pattern 'Arguments\s+=' | Select-Object -First 1
    if (-not $updaterArgs -or $updaterArgs.Line -notmatch '/MERGETASKS=autopatch') {
        throw 'The updater must pass /MERGETASKS=autopatch, or a silent install skips auto-patch setup.'
    }
}