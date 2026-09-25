param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$EnableUpdateNotifications
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$webPanelDir = Join-Path $repoRoot 'web-panel'
$solutionPath = Join-Path $repoRoot 'Wand-Enhancer.sln'

function Resolve-CommandPath {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Required command not found in PATH: $Name"
    }

    return $command.Source
}

function Resolve-VisualStudioPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found: $vswhere"
    }

    $installationPath = & $vswhere -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild -property installationPath
    if ([string]::IsNullOrWhiteSpace($installationPath)) {
        throw 'Visual Studio with MSBuild was not found.'
    }

    return $installationPath
}

function Resolve-MSBuildPath {
    param([string]$VisualStudioPath)

    $msbuildPath = Join-Path $VisualStudioPath 'MSBuild\Current\Bin\MSBuild.exe'
    if (-not (Test-Path $msbuildPath)) {
        throw "MSBuild.exe not found: $msbuildPath"
    }

    return $msbuildPath
}

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Label"
    }
}

function Resolve-TargetFrameworkRoot {
    # Some local targeting packs are installed but not registered with MSBuild.
    $root = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework'
    $frameworkList = Join-Path $root '.NETFramework\v4.8\RedistList\FrameworkList.xml'
    if (Test-Path $frameworkList) {
        return $root
    }

    return $null
}

function Resolve-NuGetPath {
    # Only needed when the NUnit console runner is missing from packages/.
    $command = Get-Command 'nuget' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $repoRoot '.nuget\nuget.exe'),
        (Join-Path $env:LOCALAPPDATA 'NuGet\nuget.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'nuget.exe not found in PATH, .nuget\, or %LOCALAPPDATA%\NuGet. Install it to restore the test runner.'
}

function Resolve-InnoSetupPath {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'ISCC.exe not found. Install Inno Setup 6, or pass the installer through CI.'
}

$pnpm = Resolve-CommandPath 'pnpm'
$visualStudio = Resolve-VisualStudioPath
$msbuild = Resolve-MSBuildPath $visualStudio
$targetFrameworkRoot = Resolve-TargetFrameworkRoot

$buildArgs = @('/m', "/p:Configuration=$Configuration", '/p:Platform=Any CPU')
if ($targetFrameworkRoot) {
    $buildArgs += "/p:TargetFrameworkRootPath=$targetFrameworkRoot"
}
if ($EnableUpdateNotifications) {
    $buildArgs += '/p:EnableUpdateNotifications=true'
}

Invoke-Step 'Install web-panel dependencies' {
    & $pnpm --dir $webPanelDir install --frozen-lockfile
}

Invoke-Step 'Lint web-panel' {
    & $pnpm --dir $webPanelDir run lint
}

Invoke-Step 'Build web-panel' {
    & $pnpm --dir $webPanelDir run build
}

Invoke-Step 'Test web-panel' {
    & $pnpm --dir $webPanelDir exec vitest run
}

Invoke-Step 'Restore NuGet packages' {
    & $msbuild $solutionPath /m /t:Restore /p:RestorePackagesConfig=true
}

Invoke-Step 'Build solution' {
    & $msbuild $solutionPath @buildArgs /t:Build
}

$assemblyPath = Join-Path $repoRoot "WandEnhancer\bin\$Configuration\WandEnhancer.exe"
Invoke-Step 'Test desktop patch state and interop' {
    & (Join-Path $repoRoot 'scripts\test-desktop.ps1') `
        -AssemblyPath $assemblyPath `
        -ExpectUpdateNotifications:$EnableUpdateNotifications
}

Invoke-Step 'Test structural patch locators' {
    & (Join-Path $repoRoot 'scripts\test-patch-locators.ps1') -AssemblyPath $assemblyPath
}

# The fork's own test suite. Upstream's build does not know about it, and without
# this step the fork's tests are built but never executed.
$forkTestDll = Join-Path $repoRoot "WandEnhancer.Core.Tests\bin\$Configuration\WandEnhancer.Core.Tests.dll"
if (Test-Path $forkTestDll) {
    Invoke-Step 'Test fork suite' {
        $nunit = Join-Path $repoRoot 'packages\NUnit.ConsoleRunner.3.16.3\tools\nunit3-console.exe'
        if (-not (Test-Path $nunit)) {
            $nuget = Resolve-NuGetPath
            & $nuget install NUnit.ConsoleRunner -Version 3.16.3 -OutputDirectory (Join-Path $repoRoot 'packages') -NonInteractive
        }
        & $nunit $forkTestDll --noresult
    }
}

# Package the installer, which is the fork's whole reason for existing.
$iscc = Resolve-InnoSetupPath
Invoke-Step 'Build installer' {
    $installerScript = Join-Path $repoRoot 'installer\WandEnhancer.iss'
    $appVersion = (Get-Content (Join-Path $repoRoot 'BuildVersion.cs') |
        Select-String -Pattern 'AssemblyFileVersion\("([\d.]+)"\)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($appVersion)) { $appVersion = '1.0.0' }
    & $iscc "/DOutputDir=$(Join-Path $repoRoot "WandEnhancer\bin\$Configuration")" "/DMyAppVersion=$appVersion" $installerScript
}

Write-Host ''
Write-Host "Build completed successfully ($Configuration)." -ForegroundColor Green
