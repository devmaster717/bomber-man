# Packs everything an offline PC needs to build the Android app, ready to copy on a USB stick:
#   BomberMan\          the source as committed (from Git, so no Library, Builds or other generated files)
#   BomberMan\Tools\offline-maven\   Gradle's libraries (made by Tools/Make-OfflineKit.ps1)
#   *.docx              the specification and offline build guide kept beside the repository
#                       ("Bomb Arena - ....docx" with a dash, so not the original design brief)
#   BomberMan.bundle    the full Git history (with -History)
#   README.txt          what is where and what to do next
# and zips it as <Out>.zip. Uncommitted changes are not included: commit first. Unity's installers are not included;
# download them separately (see the offline build guide).
param(
    [string]$Out = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path) "BombArena-offline"),
    [string]$Ref = "HEAD",
    [string[]]$Documents = @(),
    [switch]$History,
    [switch]$NoZip
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$kit = Join-Path $root "Tools\offline-maven"
if (-not (Test-Path $kit)) {
    Write-Error "Tools\offline-maven is missing: run Tools\Make-OfflineKit.ps1 first (it needs the internet once)."; exit 1
}
if (-not $Documents) {
    $Documents = @(Get-ChildItem (Split-Path $root) -Filter "Bomb Arena ? *.docx" | ForEach-Object FullName)
}

Push-Location $root
try {
    $commit = (git rev-parse --short $Ref).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    if (git status --porcelain --untracked-files=no) {
        Write-Warning "There are uncommitted changes; the package holds $Ref ($commit) without them."
    }

    if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
    New-Item -ItemType Directory $Out | Out-Null
    $source = Join-Path $Out "BomberMan"

    Write-Host "Exporting the source at $Ref ($commit)..."
    $archive = Join-Path $Out "source.zip"
    git archive --format=zip -o $archive $Ref
    if ($LASTEXITCODE -ne 0) { throw "git archive failed" }
    Expand-Archive $archive $source
    Remove-Item $archive

    Write-Host "Copying Tools\offline-maven..."
    Copy-Item -Recurse $kit (Join-Path $source "Tools\offline-maven")

    foreach ($doc in $Documents) {
        Write-Host "Copying $(Split-Path -Leaf $doc)..."
        Copy-Item $doc $Out
    }

    if ($History) {
        Write-Host "Bundling the Git history..."
        git bundle create (Join-Path $Out "BomberMan.bundle") --all
        if ($LASTEXITCODE -ne 0) { throw "git bundle failed" }
    }
} finally {
    Pop-Location
}

$historyLine = if ($History) { "BomberMan.bundle     the full Git history: git clone BomberMan.bundle BomberMan" } else { "" }
Set-Content -Encoding utf8 (Join-Path $Out "README.txt") @"
Bomb Arena - offline build package
Source: $branch at $commit, packed $(Get-Date -Format "yyyy-MM-dd HH:mm")

BomberMan\           the project (Core, DotNet, Game, Tools, docs)
BomberMan\Tools\offline-maven\   libraries for building without the internet
*.docx               specification and offline build guide
$historyLine

Not included: Unity 6000.0.84f1 and its Android Build Support module. Download their installers on a PC with
internet (Unity download archive) and install them on this PC first.

To build (PowerShell, in BomberMan):
  Tools\Build-Android.ps1 -Offline -Unity "<path to Unity.exe>"
The APK is written to Game\Builds\BombArena.apk. The first build imports the whole project and takes about
40 minutes; later builds are faster. See the offline build guide for the Unity license and troubleshooting.
"@

$size = (Get-ChildItem -Recurse -File $Out | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Package folder: {0} ({1:N0} MB)" -f $Out, $size)
if (-not $NoZip) {
    $zip = "$Out.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Write-Host "Zipping..."
    Compress-Archive -Path (Join-Path $Out "*") -DestinationPath $zip -CompressionLevel Optimal
    Write-Host ("Package zip: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}
