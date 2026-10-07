# Builds Game/Builds/BombArena.apk from the command line. Exit code 0 on success.
# -Release makes a signed Game/Builds/BombArena.aab (for Google Play) and BombArena-release.apk, reading the keystore
# from BOMBARENA_KEYSTORE, BOMBARENA_KEYSTORE_PASS, BOMBARENA_KEY_ALIAS and BOMBARENA_KEY_PASS (see issue #42).
# -Offline builds without the network: Gradle runs with --offline and takes its libraries from Tools/offline-maven
# (made by Tools/Make-OfflineKit.ps1) or, without that folder, from its cache of an earlier online build. Android's
# sdkmanager is cut off so it lists the installed SDK instead of fetching remote lists.
param(
    [string]$Unity = "D:\Unity\6000.0.84f1\Editor\Unity.exe",
    [string]$Log = "C:\Temp\bombarena-build.log",
    [switch]$Offline,
    [switch]$Release
)
$ErrorActionPreference = "Stop"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..\Game")).Path
# Java cannot create its local sockets under the user profile's Temp on this machine (see issue #1).
New-Item -ItemType Directory -Force C:\Temp | Out-Null
$env:TEMP = "C:\Temp"; $env:TMP = "C:\Temp"

if ($Offline) {
    # Gradle only takes --offline from its command line or an init script. This one acts only when the variable
    # below is set, so it is harmless for other builds on the machine.
    $gradleHome = if ($env:GRADLE_USER_HOME) { $env:GRADLE_USER_HOME } else { Join-Path $HOME ".gradle" }
    $initDir = Join-Path $gradleHome "init.d"
    New-Item -ItemType Directory -Force $initDir | Out-Null
    Set-Content -Encoding ascii (Join-Path $initDir "bombarena-offline.gradle") @'
// Installed by BomberMan's Tools/Build-Android.ps1 -Offline; does nothing unless BOMBARENA_GRADLE_OFFLINE=1.
if (System.getenv('BOMBARENA_GRADLE_OFFLINE') == '1') {
    gradle.startParameter.offline = true
    println 'BombArena: Gradle is running offline'
    def kit = System.getenv('BOMBARENA_MAVEN_KIT')
    if (kit) {
        // Searched before the project's own repositories (Google, Maven Central), which offline can't reach.
        beforeSettings { settings ->
            settings.pluginManagement.repositories { maven { url = new File(kit).toURI() } }
            settings.dependencyResolutionManagement.repositories { maven { url = new File(kit).toURI() } }
        }
        println "BombArena: libraries from $kit"
    }
}
'@
    $kit = Join-Path $PSScriptRoot "offline-maven"
    $env:BOMBARENA_MAVEN_KIT = if (Test-Path $kit) { (Resolve-Path $kit).Path } else { "" }
    $env:BOMBARENA_GRADLE_OFFLINE = "1"
    # sdkmanager would otherwise wait on remote package lists; a dead proxy makes it give up at once.
    $env:SDKMANAGER_OPTS = "-Dhttp.proxyHost=127.0.0.1 -Dhttp.proxyPort=9 -Dhttps.proxyHost=127.0.0.1 -Dhttps.proxyPort=9"
}

if ($Release) {
    foreach ($name in "BOMBARENA_KEYSTORE", "BOMBARENA_KEYSTORE_PASS", "BOMBARENA_KEY_ALIAS", "BOMBARENA_KEY_PASS") {
        if (-not [Environment]::GetEnvironmentVariable($name)) { Write-Error "-Release needs $name (see issue #42)"; exit 1 }
    }
    $env:BOMBARENA_RELEASE = "1"
} else {
    $env:BOMBARENA_RELEASE = ""
}

$p = Start-Process -FilePath $Unity -PassThru -NoNewWindow -ArgumentList @(
    "-batchmode", "-projectPath", "`"$project`"", "-buildTarget", "Android",
    "-executeMethod", "Builds.AndroidBatch", "-logFile", "`"$Log`"")
# Not Start-Process -Wait: it would also wait for the Gradle daemon, which never exits.
$p.WaitForExit()
$lines = Get-Content $Log
$lines | Select-String -Pattern "BUILD RESULT|error CS\d+|ninja: error|What went wrong|offline mode" | Select-Object -First 15 | ForEach-Object { $_.Line }
# A release builds twice (.aab, then .apk): both must succeed.
$results = @($lines | Select-String -Pattern "BUILD RESULT: ")
if ($results.Count -gt 0 -and -not ($results | Where-Object { $_.Line -notmatch "BUILD RESULT: Succeeded" })) { exit 0 } else { exit 1 }
