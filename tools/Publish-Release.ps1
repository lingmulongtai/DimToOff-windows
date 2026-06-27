param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $Configuration = "Release",

    [string] $DotNet = "dotnet"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$standaloneDir = Join-Path $repoRoot "publish\win-x64-standalone"
$smallDir = Join-Path $repoRoot "publish\win-x64-small"
$artifactDir = Join-Path $repoRoot "release\$Version"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $Arguments)

    & $DotNet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed: $($Arguments -join ' ')"
    }
}

function New-ReleaseZip {
    param(
        [string] $SourceDirectory,
        [string] $FileName
    )

    $zipPath = Join-Path $artifactDir $FileName
    $shaPath = "$zipPath.sha256"
    Remove-Item -LiteralPath $zipPath, $shaPath -Force -ErrorAction SilentlyContinue

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        (Resolve-Path $SourceDirectory),
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $FileName" | Set-Content -LiteralPath $shaPath -Encoding ascii

    Get-Item -LiteralPath $zipPath, $shaPath
}

Push-Location $repoRoot
try {
    Remove-Item -LiteralPath $standaloneDir, $smallDir, $artifactDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $standaloneDir, $smallDir, $artifactDir | Out-Null

    Invoke-DotNet build ".\DimToOff.sln" "-c" $Configuration

    Invoke-DotNet publish ".\src\DimToOff.Settings\DimToOff.Settings.csproj" "-c" $Configuration "-r" "win-x64" "--self-contained" "true" "-o" $standaloneDir "/p:WindowsAppSDKSelfContained=true" "/p:PublishSingleFile=false" "/p:PublishReadyToRun=false"
    Invoke-DotNet publish ".\src\DimToOff\DimToOff.csproj" "-c" $Configuration "-r" "win-x64" "--self-contained" "true" "-o" $standaloneDir "/p:PublishSingleFile=false" "/p:PublishReadyToRun=false"

    Invoke-DotNet publish ".\src\DimToOff.Settings\DimToOff.Settings.csproj" "-c" $Configuration "-r" "win-x64" "--self-contained" "false" "-o" $smallDir "/p:WindowsAppSDKSelfContained=false" "/p:PublishSingleFile=false" "/p:PublishReadyToRun=false"
    Invoke-DotNet publish ".\src\DimToOff\DimToOff.csproj" "-c" $Configuration "-r" "win-x64" "--self-contained" "false" "-o" $smallDir "/p:PublishSingleFile=false" "/p:PublishReadyToRun=false"

    $standaloneZip = "DimToOff-$Version-win-x64.zip"
    $smallZip = "DimToOff-$Version-win-x64-small.zip"

    New-ReleaseZip -SourceDirectory $standaloneDir -FileName $standaloneZip
    New-ReleaseZip -SourceDirectory $smallDir -FileName $smallZip
}
finally {
    Pop-Location
}
