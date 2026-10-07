# Packs everything an offline PC needs to build the Android app, ready to copy on a USB stick:
#   BomberMan\          a Git clone of the project: .git with the full history, and the committed files checked out
#                       (no Library, Builds or other generated files)
#   BomberMan\Tools\offline-maven\   Gradle's libraries (made by Tools/Make-OfflineKit.ps1; ignored by Git)
#   *.docx              the specification and offline build guide kept beside the repository
#                       ("Bomb Arena - ....docx" with a dash, so not the original design brief)
#   README.txt          what is where and what to do next
# and zips it as <Out>.zip. Uncommitted changes are not included: commit first. Unity's installers are not included;
# download them separately (see the offline build guide).
param(
    [string]$Out = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path) "BombArena-offline"),
    [string]$Ref = "HEAD",
    [string[]]$Documents = @(),
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
    $origin = (git remote get-url origin).Trim()
    if (git status --porcelain --untracked-files=no) {
        Write-Warning "There are uncommitted changes; the package holds $Ref ($commit) without them."
    }
} finally {
    Pop-Location
}

if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory $Out | Out-Null
$source = Join-Path $Out "BomberMan"

# A real clone (copying objects, not linking them), so the package has its own .git with every commit and tag.
Write-Host "Cloning the project at $Ref ($commit)..."
git clone --quiet --no-local $root $source
if ($LASTEXITCODE -ne 0) { throw "git clone failed" }
Push-Location $source
try {
    if ($Ref -ne "HEAD") {
        git checkout --quiet $Ref
        if ($LASTEXITCODE -ne 0) { throw "git checkout $Ref failed" }
    }
    # Point the clone at GitHub, as the original is, rather than at this PC's folder.
    git remote set-url origin $origin
} finally {
    Pop-Location
}

Write-Host "Copying Tools\offline-maven..."
Copy-Item -Recurse $kit (Join-Path $source "Tools\offline-maven")

foreach ($doc in $Documents) {
    Write-Host "Copying $(Split-Path -Leaf $doc)..."
    Copy-Item $doc $Out
}

Set-Content -Encoding utf8 (Join-Path $Out "README.txt") @"
Bomb Arena - offline build package
Source: $branch at $commit, packed $(Get-Date -Format "yyyy-MM-dd HH:mm")

BomberMan\           the project as a Git repository (.git holds the full history; remote: $origin)
BomberMan\Tools\offline-maven\   libraries for building without the internet
*.docx               specification and offline build guide

Not included: Unity 6000.0.84f1 and its Android Build Support module. Download their installers on a PC with
internet (Unity download archive) and install them on this PC first.

To build (PowerShell, in BomberMan):
  Tools\Build-Android.ps1 -Offline -Unity "<path to Unity.exe>"
The APK is written to Game\Builds\BombArena.apk. The first build imports the whole project and takes about
40 minutes; later builds are faster. See the offline build guide for the Unity license and troubleshooting.
"@

$size = (Get-ChildItem -Recurse -File -Force $Out | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Package folder: {0} ({1:N0} MB)" -f $Out, $size)
if (-not $NoZip) {
    $zip = "$Out.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Write-Host "Zipping..."
    # Compress-Archive leaves out hidden items, and Git marks .git hidden, so every file is added by hand, with
    # forward slashes in its path (Windows PowerShell's ZipFile would write backslashes, which other unzip tools
    # don't read as folders).
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -Recurse -File -Force $Out) {
            $entry = $file.FullName.Substring($Out.Length + 1).Replace("\", "/")
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally {
        $archive.Dispose()
    }
    Write-Host ("Package zip: {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}
