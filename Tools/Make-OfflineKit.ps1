# Fills Tools/offline-maven with every library Gradle needs to build the Android app, so a machine that has never
# been online can build with Build-Android.ps1 -Offline. Run it on a machine with internet, after one normal
# Build-Android.ps1 (which writes the Gradle project this script builds), and again after upgrading Unity.
# It builds a copy of that project with an empty Gradle cache, so the kit holds exactly what the build downloads.
param(
    [string]$Unity = "D:\Unity\6000.0.84f1\Editor\Unity.exe",
    [string]$Work = "C:\Temp\bombarena-kit"
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$exported = Join-Path $root "Game\Library\Bee\Android\Prj\IL2CPP\Gradle"
if (-not (Test-Path (Join-Path $exported "settings.gradle"))) {
    Write-Error "No exported Gradle project: run Tools/Build-Android.ps1 once first."; exit 1
}
$android = Join-Path (Split-Path $Unity) "Data\PlaybackEngines\AndroidPlayer"
$launcher = Get-ChildItem (Join-Path $android "Tools\gradle\lib\gradle-launcher-*.jar") | Select-Object -First 1
New-Item -ItemType Directory -Force C:\Temp | Out-Null
$env:TEMP = "C:\Temp"; $env:TMP = "C:\Temp"

if (Test-Path $Work) { Remove-Item -Recurse -Force $Work }
New-Item -ItemType Directory $Work | Out-Null
$project = Join-Path $Work "prj"
$gradleHome = Join-Path $Work "home"
Write-Host "Copying the Gradle project..."
Copy-Item -Recurse $exported $project
# A release export names the real keystore; the kit only needs the libraries, so build it unsigned.
$gradleFile = Join-Path $project "launcher\build.gradle"
$text = Get-Content -Raw $gradleFile
$text = $text -replace "(?s)signingConfigs \{.*?\n    \}\r?\n", "" -replace "\n\s*signingConfig signingConfigs\.release", ""
Set-Content -NoNewline -Encoding ascii $gradleFile $text

Write-Host "Building with an empty Gradle cache (downloads everything once)..."
$env:JAVA_HOME = Join-Path $android "OpenJDK"
Push-Location $project
& (Join-Path $env:JAVA_HOME "bin\java.exe") -classpath $launcher.FullName org.gradle.launcher.GradleMain `
    --gradle-user-home $gradleHome --no-daemon -x lint assembleRelease bundleRelease assembleDebug
$ok = $LASTEXITCODE -eq 0
Pop-Location
if (-not $ok) { Write-Error "The Gradle build failed; see the output above."; exit 1 }

# Gradle's cache is files-2.1/<group>/<module>/<version>/<hash>/<file>; a Maven folder is <group as path>/<module>/<version>/<file>.
Write-Host "Writing Tools/offline-maven..."
$kit = Join-Path $root "Tools\offline-maven"
if (Test-Path $kit) { Remove-Item -Recurse -Force $kit }
$cache = Join-Path $gradleHome "caches\modules-2\files-2.1"
$count = 0
foreach ($file in Get-ChildItem -Recurse -File $cache) {
    $parts = $file.FullName.Substring($cache.Length + 1).Split("\")
    $group, $module, $version = $parts[0], $parts[1], $parts[2]
    $target = Join-Path $kit (Join-Path ($group.Replace(".", "\")) (Join-Path $module $version))
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item $file.FullName (Join-Path $target $file.Name)
    $count++
}
$size = (Get-ChildItem -Recurse -File $kit | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Done: {0} files, {1:N0} MB in Tools/offline-maven." -f $count, $size)
Remove-Item -Recurse -Force $Work
