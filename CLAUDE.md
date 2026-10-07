## Layout

- `Core/`: the game rules as plain C# with no Unity references (ADR 0001). It's also a local Unity package (`com.bombarena.core`). Positions are integers (1 tile = 1000 units) on a 20-tick clock, so results are deterministic.
- `DotNet/`: builds `Core/` for .NET Standard 2.1 / C# 9 (Unity's level) and holds its NUnit tests.
- `Game/`: the Unity 6 project. It only reads input, steps the core and draws the core's state.

## Build and test

- Core tests: `dotnet test BombArena.sln`
- Android APK: `Tools/Build-Android.ps1`. It writes `Game/Builds/BombArena.apk` and exits non-zero on failure. Underneath it runs Unity in batch mode with `-executeMethod Builds.AndroidBatch`.
- Release: `Tools/Build-Android.ps1 -Release` makes a signed `Game/Builds/BombArena.aab` (Google Play) and `BombArena-release.apk`, with the keystore from `BOMBARENA_KEYSTORE`, `BOMBARENA_KEYSTORE_PASS`, `BOMBARENA_KEY_ALIAS` and `BOMBARENA_KEY_PASS` (issue #42). The version is `Builds.Version` / `Builds.VersionCode`; raise the code for every upload.
- Offline: `Tools/Build-Android.ps1 -Offline` builds without the network (Gradle runs `--offline`; sdkmanager is cut off), taking Gradle's libraries from `Tools/offline-maven` (made by `Tools/Make-OfflineKit.ps1`, not in Git) or else from the cache of an earlier online build. A machine that has never been online can build: see `docs/offline-build.md`. The Android SDK, NDK and JDK ship inside Unity, URP is bundled, and the target API is pinned so Unity doesn't look it up online.
- Run on LDPlayer: `Tools/Run-LDPlayer.ps1` installs and launches the APK using LDPlayer's own adb (device `emulator-5554`). Machine-specific gotchas are in the closing comment of issue #1.

## Agent skills

### Issue tracker

Issues are tracked in this repo's GitHub Issues, using the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Uses the five default triage labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
