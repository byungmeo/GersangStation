[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$MSBuildPath = 'MSBuild.exe'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') {
    throw 'Use a four-part Store version ending in .0 (for example, 2.0.10.0).'
}
$packageVersion = [version]$Version
if ($packageVersion.Major -lt 1 -or $packageVersion.Major -gt 65535 -or
    $packageVersion.Minor -gt 65535 -or $packageVersion.Build -gt 65535) {
    throw 'MSIX version fields must be within 0..65535, with a nonzero major version.'
}
$repository = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and
    @(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) {
    throw 'Use an empty output directory to avoid submitting a stale package.'
}
$null = New-Item -ItemType Directory -Force -Path $output
$manifestPath = Join-Path $repository 'GersangStation.WinUI/GersangStation/Package.appxmanifest'
$originalBytes = [IO.File]::ReadAllBytes($manifestPath)
$originalText = [IO.File]::ReadAllText($manifestPath)
$identityVersion = [regex]'(?s)(<Identity\b[^>]*\bVersion=")[^"]+("[^>]*>)'
if ($identityVersion.Matches($originalText).Count -ne 1) {
    throw 'Expected exactly one package Identity version in the Store manifest.'
}
$updatedText = $identityVersion.Replace($originalText, {
    param($match)
    $match.Groups[1].Value + $packageVersion.ToString(4) + $match.Groups[2].Value
})
$hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 239 -and
    $originalBytes[1] -eq 187 -and $originalBytes[2] -eq 191
$sourceCommit = & git -C $repository rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the source commit for this package.' }
$sourceStatus = @(& git -C $repository status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine whether the source checkout is clean.' }

try {
    [IO.File]::WriteAllText($manifestPath, $updatedText, [Text.UTF8Encoding]::new($hasBom))
    # Rebuild clears the SDK's cached upload-bundle manifest when versions change.
    & $MSBuildPath (Join-Path $repository 'GersangStation.slnx') /restore /t:Rebuild /m /nologo `
        /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true `
        /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always /p:AppxBundlePlatforms=x64 `
        /p:AppxPackageSigningEnabled=false /p:AppxAutoIncrementPackageRevision=false `
        "/p:AppxPackageDir=$output/" "/bl:$output/build.binlog"
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }
}
finally {
    [IO.File]::WriteAllBytes($manifestPath, $originalBytes)
}

$packages = @(Get-ChildItem -LiteralPath $output -Recurse -File |
    Where-Object { $_.Extension -in '.msixupload', '.appxupload' })
if ($packages.Count -ne 1) {
    throw "Expected one Store upload package, found $($packages.Count)."
}
$package = $packages[0]
$hash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
"$hash  $($package.Name)" | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8
[xml]$storeManifest = $updatedText
[ordered]@{
    schemaVersion = 1
    version = $packageVersion.ToString(4)
    sourceCommit = $sourceCommit.Trim()
    sourceDirty = $sourceStatus.Count -gt 0
    packageIdentityName = $storeManifest.Package.Identity.Name
    publisher = $storeManifest.Package.Identity.Publisher
    packageFile = $package.Name
    sha256 = $hash
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
    githubRunId = $env:GITHUB_RUN_ID
    githubRunAttempt = $env:GITHUB_RUN_ATTEMPT
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'release-build.json') -Encoding utf8
Write-Host "Store upload package: $($package.FullName)"
Write-Host "SHA256: $hash"
if ($env:GITHUB_OUTPUT) {
    "package=$($package.FullName)" >> $env:GITHUB_OUTPUT
}
