# Installs Game/Builds/BombArena.apk on LDPlayer (starting it if needed) and launches the game.
param([string]$LDPlayer = "D:\LDPlayer\LDPlayer14", [string]$Device = "emulator-5554")
$adb = Join-Path $LDPlayer "adb.exe"
$console = Join-Path $LDPlayer "ldconsole.exe"
$apk = (Resolve-Path (Join-Path $PSScriptRoot "..\Game\Builds\BombArena.apk")).Path
if (-not ((& $console isrunning --index 0) -match "running")) { & $console launch --index 0 }
# Use only LDPlayer's adb: mixing it with Unity's newer adb makes the emulator show as offline.
for ($i = 0; $i -lt 60; $i++) {
    if ((& $adb devices) -match "$Device\s+device") {
        if ("$(& $adb -s $Device shell getprop sys.boot_completed)".Trim() -eq "1") { break }
    }
    Start-Sleep 2
}
& $adb -s $Device install -r "$apk"
& $adb -s $Device shell am force-stop com.bombarena.game
& $adb -s $Device shell am start -W -n com.bombarena.game/com.unity3d.player.UnityPlayerGameActivity
