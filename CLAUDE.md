## Layout

- `Core/`: the game rules as plain C# with no Unity references (ADR 0001). It's also a local Unity package (`com.bombarena.core`). Positions are integers (1 tile = 1000 units) on a 20-tick clock, so results are deterministic.
- `DotNet/`: builds `Core/` for .NET Standard 2.1 / C# 9 (Unity's level) and holds its NUnit tests.
- `Game/`: the Unity 6 project. It only reads input, steps the core and draws the core's state.

## Build and test

- Core tests: `dotnet test BombArena.sln`
- Android APK: `D:\Unity\6000.0.84f1\Editor\Unity.exe -batchmode -projectPath Game -buildTarget Android -executeMethod Builds.AndroidBatch -logFile <log>`. The output is `Game/Builds/BombArena.apk`; look for `BUILD RESULT` in the log. If you launch it from PowerShell, wait with `$p.WaitForExit()`, because `Start-Process -Wait` also waits for the Gradle daemon and never returns.
- Run on LDPlayer with its own adb (`D:\LDPlayer\LDPlayer14\adb.exe`, device `emulator-5554`), then `am start -n com.bombarena.game/com.unity3d.player.UnityPlayerGameActivity`. Machine-specific gotchas are in the closing comment of issue #1.

## Agent skills

### Issue tracker

Issues are tracked in this repo's GitHub Issues, using the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the five default triage labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
