[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [string]$Configuration = 'Release',

    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [string]$MsBuildPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe',

    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$winformsRoot = Split-Path -Parent $scriptRoot
$repoRoot = Split-Path -Parent $winformsRoot
$distributionRoot = Join-Path $winformsRoot 'Publish'
$projectRoot = Join-Path $winformsRoot 'GersangStation'
$projectPath = Join-Path $projectRoot 'GersangStation.Winform.csproj'
$publishProfile = 'FolderRelease_win-x64'
$publishBase = Join-Path $distributionRoot 'staging'
$releaseFolderName = "거상 스테이션 미니 v$Version"
$expectedReleaseRoot = Join-Path $publishBase $releaseFolderName
$releaseRoot = $null
$appRoot = $null
$zipPath = Join-Path $distributionRoot "GersangStation_mini_v.$Version.zip"
$licenseSourcePath = Join-Path $repoRoot 'LICENSE'
$guideSourceRoot = Join-Path $projectRoot 'Properties\PublishProfiles\Includes'

try {
    if (-not (Test-Path $MsBuildPath)) {
        throw "MSBuild를 찾을 수 없습니다: $MsBuildPath"
    }

    if (-not (Test-Path $distributionRoot)) {
        New-Item -ItemType Directory -Path $distributionRoot | Out-Null
    }

    $resolvedStagingRoot = [IO.Path]::GetFullPath($publishBase) + [IO.Path]::DirectorySeparatorChar
    $resolvedReleaseRoot = [IO.Path]::GetFullPath($expectedReleaseRoot)
    if (-not $resolvedReleaseRoot.StartsWith($resolvedStagingRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Release output must stay inside the staging directory.'
    }
    if (Test-Path -LiteralPath $resolvedReleaseRoot) {
        Remove-Item -LiteralPath $resolvedReleaseRoot -Recurse -Force
    }
    $publishDestination = Join-Path $expectedReleaseRoot 'GersangStationMini'

    Write-Host "Publishing GersangStationMini v$Version..." -ForegroundColor Cyan
    & $MsBuildPath $projectPath /restore /t:Publish /p:Configuration=$Configuration /p:PublishProfile=$publishProfile /p:Version=$Version /p:RuntimeIdentifier=$RuntimeIdentifier /p:Platform=x64 /p:SelfContained=false /p:PublishDir="$publishDestination\"
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild Publish failed with exit code $LASTEXITCODE."
    }

    $releaseDirectory = Get-ChildItem -LiteralPath $publishBase -Directory |
        Where-Object { $_.Name -like "*v$Version" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $releaseDirectory) {
        throw "출시 폴더를 찾을 수 없습니다: $expectedReleaseRoot"
    }

    $releaseRoot = $releaseDirectory.FullName
    $appRoot = Join-Path $releaseRoot 'GersangStationMini'

    if (-not (Test-Path $appRoot)) {
        throw "출시 폴더를 찾을 수 없습니다: $appRoot"
    }

    if (-not (Test-Path $licenseSourcePath)) {
        throw "LICENSE 파일을 찾을 수 없습니다: $licenseSourcePath"
    }

    if (-not (Test-Path $guideSourceRoot)) {
        throw "안내 파일 소스 폴더를 찾을 수 없습니다: $guideSourceRoot"
    }

    $guideSourceFiles = Get-ChildItem -LiteralPath $guideSourceRoot -File -Filter '*.url' |
        Sort-Object Name

    if ($guideSourceFiles.Count -lt 4) {
        throw "안내 파일이 부족합니다: $guideSourceRoot"
    }

    $mainExePath = Join-Path $appRoot 'GersangStation.exe'
    if (-not (Test-Path $mainExePath)) {
        throw "메인 실행 파일을 찾을 수 없습니다: $mainExePath"
    }

    foreach ($guideSourceFile in $guideSourceFiles) {
        Copy-Item -Force $guideSourceFile.FullName (Join-Path $releaseRoot $guideSourceFile.Name)
    }

    $updatorOutputRoot = Join-Path $appRoot 'Updator'
    if (-not (Test-Path $updatorOutputRoot)) {
        New-Item -ItemType Directory -Path $updatorOutputRoot | Out-Null
    }

    $updatorFiles = @(
        'GersangStationMiniUpdator.exe',
        'GersangStationMiniUpdator.dll',
        'GersangStationMiniUpdator.deps.json',
        'GersangStationMiniUpdator.runtimeconfig.json'
    )

    foreach ($updatorFile in $updatorFiles) {
        $updatorPublishedPath = Join-Path $updatorOutputRoot $updatorFile
        if (-not (Test-Path $updatorPublishedPath)) {
            throw "Updator 파일을 찾을 수 없습니다: $updatorPublishedPath"
        }
    }

    Copy-Item -Force $licenseSourcePath (Join-Path $appRoot 'LICENSE')

    $configFilesToRemove = @(
        (Join-Path $appRoot 'GersangStation.dll.config'),
        (Join-Path $appRoot 'GersangStation.exe.config')
    )

    foreach ($configFilePath in $configFilesToRemove) {
        if (Test-Path $configFilePath) {
            Remove-Item -Force $configFilePath
        }
    }

    foreach ($guideSourceFile in $guideSourceFiles) {
        $guidePath = Join-Path $releaseRoot $guideSourceFile.Name
        if (-not (Test-Path $guidePath)) {
            throw "안내 파일을 찾을 수 없습니다: $guidePath"
        }
    }

    $updatorExePath = Join-Path $appRoot 'Updator\GersangStationMiniUpdator.exe'
    if (-not (Test-Path $updatorExePath)) {
        throw "Updator 실행 파일을 찾을 수 없습니다: $updatorExePath"
    }

    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath
    }

    Write-Host "Creating zip package..." -ForegroundColor Cyan
    Compress-Archive -LiteralPath $releaseRoot -DestinationPath $zipPath -CompressionLevel Optimal

    Write-Host ''
    Write-Host "Release root : $releaseRoot" -ForegroundColor Green
    Write-Host "Zip package  : $zipPath" -ForegroundColor Green
}
catch {
    Write-Error $_
}
finally {
    if (-not $NoPause) { [void](Read-Host 'Done. Press Enter to close this window') }
}
