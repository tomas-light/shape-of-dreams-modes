param(
    [string] $GameDir = $env:SOD_GAME_DIR
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Shape of Dreams"
}

$modRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $modRoot)
$artifacts = Join-Path $repoRoot 'artifacts'
$source = Join-Path $modRoot 'src\EffectBreakdown'
$metadata = Join-Path $modRoot 'mod\about'
$version = (Get-Content -LiteralPath (Join-Path $metadata 'metadata.json') -Raw | ConvertFrom-Json).modVer

dotnet build (Join-Path $source 'EffectBreakdown.csproj') --configuration Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'EffectBreakdown build failed.' }

$mod = Join-Path $artifacts 'EffectBreakdown'
$about = Join-Path $mod 'about'
$library = Join-Path $mod 'lib'
New-Item -ItemType Directory -Force -Path $about, $library | Out-Null
foreach ($file in @('metadata.json', 'description.txt', 'preview.png', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $metadata $file) -Destination $about
}
Copy-Item -LiteralPath (Join-Path $source 'bin\Release\netstandard2.1\EffectBreakdown.dll') -Destination $library

$zip = Join-Path $artifacts "EffectBreakdown-$version.zip"
Compress-Archive -LiteralPath $mod -DestinationPath $zip -Force
Write-Output $zip

