# Builds Game/Builds/BombArena.apk from the command line. Exit code 0 on success.
# -Offline builds without the network (after one online build has filled Gradle's cache): Gradle runs with
# --offline and Android's sdkmanager is cut off so it lists the installed SDK instead of fetching remote lists.
param(
    [string]$Unity = "D:\Unity\6000.0.84f1\Editor\Unity.exe",
    [string]$Log = "C:\Temp\bombarena-build.log",
    [switch]$Offline
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
}
'@
    $env:BOMBARENA_GRADLE_OFFLINE = "1"
    # sdkmanager would otherwise wait on remote package lists; a dead proxy makes it give up at once.
    $env:SDKMANAGER_OPTS = "-Dhttp.proxyHost=127.0.0.1 -Dhttp.proxyPort=9 -Dhttps.proxyHost=127.0.0.1 -Dhttps.proxyPort=9"
}

$p = Start-Process -FilePath $Unity -PassThru -NoNewWindow -ArgumentList @(
    "-batchmode", "-projectPath", "`"$project`"", "-buildTarget", "Android",
    "-executeMethod", "Builds.AndroidBatch", "-logFile", "`"$Log`"")
# Not Start-Process -Wait: it would also wait for the Gradle daemon, which never exits.
$p.WaitForExit()
$lines = Get-Content $Log
$lines | Select-String -Pattern "BUILD RESULT|error CS\d+|ninja: error|What went wrong|offline mode" | Select-Object -First 15 | ForEach-Object { $_.Line }
if ($lines | Select-String -SimpleMatch "BUILD RESULT: Succeeded") { exit 0 } else { exit 1 }
