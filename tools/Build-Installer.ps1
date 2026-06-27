param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $SourceDir,

    [string] $OutputDir,

    [string] $InnoSetupCompiler
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($SourceDir)) {
    $SourceDir = Join-Path $repoRoot "publish\win-x64-standalone"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "release\installer"
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
    )
    $InnoSetupCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
        $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
        if ($command) {
            $InnoSetupCompiler = $command.Source
        }
    }
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or -not (Test-Path $InnoSetupCompiler)) {
    throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 or pass -InnoSetupCompiler."
}

if (-not (Test-Path (Join-Path $SourceDir "DimToOff.exe")) -or
    -not (Test-Path (Join-Path $SourceDir "DimToOff.Settings.exe"))) {
    throw "Published app files were not found in '$SourceDir'. Run tools\Publish-Release.ps1 first."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$appVersion = $Version -replace '^[vV]', ''
$scriptPath = Join-Path $repoRoot "installer\DimToOff.iss"

& $InnoSetupCompiler `
    $scriptPath `
    "/DAppVersion=$appVersion" `
    "/DPackageVersion=$Version" `
    "/DSourceDir=$SourceDir" `
    "/DOutputDir=$OutputDir"

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

Get-ChildItem -LiteralPath $OutputDir -Filter "DimToOff-$Version-setup.exe"
