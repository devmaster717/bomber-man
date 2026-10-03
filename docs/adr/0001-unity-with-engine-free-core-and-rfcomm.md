# Unity with an engine-free C# game core, and classic Bluetooth RFCOMM

We build on Unity 6, but all game rules live in a plain C# core that never references `UnityEngine`; Unity only reads inputs and draws the core's state. This keeps the spec's promise that the 2D view can later be swapped for a 3D one without touching gameplay, and lets the rules be tested and played instantly in the Unity Editor. Bluetooth battles use classic Bluetooth RFCOMM sockets through a small Java Android plugin, with the host's device running the core and guests sending only inputs.

## Considered Options

- **Godot 4.7**: official billing plugin and an easy 3D path, but rejected in favour of Unity.
- **Java game core called from Unity over JNI**: rejected because Unity's Play mode cannot run Java, so every gameplay change would need an APK build and an emulator or phone. Java is kept for the Android-only edge (Bluetooth, later billing).
- **Google Nearby Connections / BLE**: rejected for classic RFCOMM, which needs no Play Services and is the more conventional mechanism; no maintained Unity plugin existed for either, so a hand-written plugin was needed anyway.

## Consequences

- Bluetooth cannot be tested in the editor or reliably on emulators (LDPlayer's adapter is virtual); battle testing needs real phones.
- Guests see every action one round trip late; no client-side prediction until playtests show it is needed.
