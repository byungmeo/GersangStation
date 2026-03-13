[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [string]$Configuration = 'Release',

    [string]$RuntimeIdentifier = 'win-x64',

    [string]$MsBuildPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$legacyRoot = Split-Path -Parent $scriptRoot
$repoRoot = Split-Path -Parent $legacyRoot
$distributionRoot = Join-Path $legacyRoot 'Publish'
$projectRoot = Join-Path $legacyRoot 'GersangStation'
$projectPath = Join-Path $projectRoot 'GersangStation.csproj'
$publishProfile = 'FolderRelease_win-x64'
$targetFramework = 'net6.0-windows7.0'
$publishBase = Join-Path $projectRoot "bin\$Configuration\$targetFramework\publish"
$releaseFolderName = "거상 스테이션 v$Version"
$expectedReleaseRoot = Join-Path $publishBase $releaseFolderName
$releaseRoot = $null
$appRoot = $null
$zipPath = Join-Path $distributionRoot "GersangStation_v.$Version.zip"
$licenseSourcePath = Join-Path $repoRoot 'LICENSE'
$guideSourceRoot = Join-Path $projectRoot 'Properties\PublishProfiles\Includes'
$updatorBuildRoot = Join-Path $legacyRoot "GersangStationMiniUpdator\bin\$Configuration\net6.0-windows7.0"

try {
    if (-not (Test-Path $MsBuildPath)) {
        throw "MSBuild를 찾을 수 없습니다: $MsBuildPath"
    }

    if (-not (Test-Path $distributionRoot)) {
        New-Item -ItemType Directory -Path $distributionRoot | Out-Null
    }

    if (Test-Path $expectedReleaseRoot) {
        Remove-Item -Recurse -Force $expectedReleaseRoot
    }

    Write-Host "Publishing GersangStationMini v$Version..." -ForegroundColor Cyan
    & $MsBuildPath $projectPath /restore /t:Publish /p:Configuration=$Configuration /p:PublishProfile=$publishProfile /p:Version=$Version /p:RuntimeIdentifier=$RuntimeIdentifier
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
    $appRoot = Join-Path $releaseRoot 'GersangStation'

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

    if (-not (Test-Path $updatorBuildRoot)) {
        throw "Updator 빌드 폴더를 찾을 수 없습니다: $updatorBuildRoot"
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
        $updatorSourcePath = Join-Path $updatorBuildRoot $updatorFile
        if (-not (Test-Path $updatorSourcePath)) {
            throw "Updator 파일을 찾을 수 없습니다: $updatorSourcePath"
        }

        Copy-Item -Force $updatorSourcePath (Join-Path $updatorOutputRoot $updatorFile)
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
    [void](Read-Host 'Done. Press Enter to close this window')
}
