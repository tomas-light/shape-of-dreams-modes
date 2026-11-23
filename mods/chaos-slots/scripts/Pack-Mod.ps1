param(
    [string] $GameDir = $env:SOD_GAME_DIR,
    [switch] $IncludeTestRoom = $true
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Shape of Dreams"
}
$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$artifacts = Join-Path $repoRoot 'artifacts'

function Pack-Mod([string] $ProjectName, [string] $PackageName, [string] $AboutPath) {
    $source = Join-Path $modRoot "src\$ProjectName"
    $project = Join-Path $source "$ProjectName.csproj"
    $assembly = Join-Path $source "bin\Release\netstandard2.1\$ProjectName.dll"
    $metadata = Join-Path $modRoot $AboutPath
    $version = (Get-Content -LiteralPath (Join-Path $metadata 'metadata.json') -Raw | ConvertFrom-Json).modVer

    dotnet build $project --configuration Release "-p:GameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "$ProjectName build failed." }

    $mod = Join-Path $artifacts $PackageName
    $about = Join-Path $mod 'about'
    $library = Join-Path $mod 'lib'
    New-Item -ItemType Directory -Force -Path $about, $library | Out-Null
    foreach ($file in @('metadata.json', 'description.txt')) {
        Copy-Item -LiteralPath (Join-Path $metadata $file) -Destination $about
    }
    $preview = Join-Path $metadata 'preview.png'
    if (Test-Path -LiteralPath $preview -PathType Leaf) {
        Copy-Item -LiteralPath $preview -Destination $about
    }
    Copy-Item -LiteralPath $assembly -Destination $library

    $zip = Join-Path $artifacts "$PackageName-$version.zip"
    Compress-Archive -LiteralPath $mod -DestinationPath $zip -Force
    Write-Output $zip
}

Pack-Mod 'ChaosSlots' 'ChaosSlots' 'mod\about'
if ($IncludeTestRoom) {
    Pack-Mod 'ChaosSlots.QaRoom' 'ChaosSlotsTestRoom' 'mod\qa-room\about'
}
