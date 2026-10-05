# Builds Game/Builds/BombArena.apk from the command line. Exit code 0 on success.
param(
    [string]$Unity = "D:\Unity\6000.0.84f1\Editor\Unity.exe",
    [string]$Log = "C:\Temp\bombarena-build.log"
)
$ErrorActionPreference = "Stop"
$project = (Resolve-Path (Join-Path $PSScriptRoot "..\Game")).Path
# Java cannot create its local sockets under the user profile's Temp on this machine (see issue #1).
New-Item -ItemType Directory -Force C:\Temp | Out-Null
$env:TEMP = "C:\Temp"; $env:TMP = "C:\Temp"
$p = Start-Process -FilePath $Unity -PassThru -NoNewWindow -ArgumentList @(
    "-batchmode", "-projectPath", "`"$project`"", "-buildTarget", "Android",
    "-executeMethod", "Builds.AndroidBatch", "-logFile", "`"$Log`"")
# Not Start-Process -Wait: it would also wait for the Gradle daemon, which never exits.
$p.WaitForExit()
$lines = Get-Content $Log
$lines | Select-String -Pattern "BUILD RESULT|error CS\d+|ninja: error|What went wrong" | Select-Object -First 15 | ForEach-Object { $_.Line }
if ($lines | Select-String -SimpleMatch "BUILD RESULT: Succeeded") { exit 0 } else { exit 1 }
