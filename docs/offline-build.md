# Building on a machine without internet

The Android app builds on a Windows PC that is never online. Everything it needs is copied over once.

## What the offline PC needs

- **Windows 10 (21H1 or later) or 11, 64-bit** with a DirectX 11 or 12 graphics card: Unity 6's minimum, so
  Windows 7 and 8 can't run it. 16 GB RAM and roughly 25 GB of free disk are comfortable.
- **Unity 6000.0.84f1 with Android Build Support** (which includes the OpenJDK, Android SDK and NDK). Download the
  editor and Android installers on an online PC from the Unity download archive and run them on the offline PC; the
  Android SDK and NDK come inside them.
- **A Unity license.** Unity Personal can only be activated by signing in to Unity Hub, so the PC needs internet once
  for that (or activate it elsewhere while it still has a connection). After activation Unity runs without the
  network: the offline test build reported the license as valid with no network.
- **This repository**, plus the **`Tools/offline-maven` folder** (about 330 MB, not in Git): the libraries Android's
  Gradle build would otherwise download.

Nothing else: Unity's packages (URP and the rest) ship inside the editor, and the project's art, music and fonts are in
the repository.

## Making `Tools/offline-maven`

On a PC with internet, after one normal build (`Tools/Build-Android.ps1`), run:

```
Tools/Make-OfflineKit.ps1
```

It builds the app's Gradle project once with an empty cache and stores everything that build downloaded in
`Tools/offline-maven`. Copy that folder into the same place in the offline PC's copy of the repository. Run it again
after upgrading Unity.

## Packing it for the offline PC

```
Tools/Pack-Offline.ps1
```

It clones the repository into the package (`.git` with the full history, the committed files checked out, no
`Library` or build output: about 45 MB), adds `Tools/offline-maven` and the specification and offline build guide (the
`Bomb Arena — ….docx` files next to the project folder), and zips it all as `BombArena-offline.zip` next to the
project folder. The full, illustrated steps, including the Unity license, are in `Bomb Arena — Offline Build
Guide.docx`.

## Building

```
Tools/Build-Android.ps1 -Offline
Tools/Build-Android.ps1 -Offline -Release
```

The first build on a new PC imports the whole project and compiles the engine code, which takes about 40 minutes;
later builds are much faster.

## How it was checked

A fresh clone (no Unity `Library` folder) was built with `-Offline` using an empty Gradle cache, an empty Unity
package cache and the network cut off through a dead proxy. The build succeeded, and Gradle's cache was still empty
afterwards: every library came from `Tools/offline-maven`.
