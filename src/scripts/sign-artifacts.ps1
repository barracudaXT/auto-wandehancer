# Authenticode-signs build artifacts. No-op when no certificate is supplied, so
# unsigned local and CI builds keep working unchanged.
#
#   ./scripts/sign-artifacts.ps1 -Files a.exe,b.exe -PfxPath cert.pfx -PfxPassword pw
#
# A trusted CA certificate (OV/EV) is what actually clears the Windows SmartScreen
# warning. A self-signed certificate only makes the signature verifiable by machines
# that already trust it, so it does not remove the warning for end users.

param(
    [Parameter(Mandatory = $true)]
    [string[]]$Files,

    [string]$PfxPath,

    [string]$PfxPassword,

    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PfxPath)) {
    Write-Host 'No signing certificate supplied - artifacts left unsigned.' -ForegroundColor Yellow
    return
}

if (-not (Test-Path $PfxPath)) {
    throw "Signing certificate not found: $PfxPath"
}

function Resolve-SignToolPath {
    $candidates = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object { [version]($_.Directory.Parent.Name) } -Descending

    if ($candidates) {
        return $candidates[0].FullName
    }

    $onPath = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    throw 'signtool.exe not found. Install the Windows SDK signing tools.'
}

$signTool = Resolve-SignToolPath

foreach ($file in $Files) {
    if (-not (Test-Path $file)) {
        throw "Artifact to sign not found: $file"
    }

    Write-Host "Signing $file" -ForegroundColor Cyan
    & $signTool sign /f $PfxPath /p $PfxPassword /fd sha256 /tr $TimestampUrl /td sha256 $file
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $file (exit $LASTEXITCODE)"
    }

    & $signTool verify /pa $file
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed for $file (exit $LASTEXITCODE)"
    }
}

Write-Host 'Signing complete.' -ForegroundColor Green
