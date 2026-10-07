# The palace look: URP, CC0 art, and 2D sprites rendered from the 3D models

The game moved from flat placeholder shapes to a "royal palace" look in both views: marble, stone, wood, gold and carpet from CC0 photo textures (ambientCG), models built in code from bevelled boxes and primitives, and the Cinzel Decorative and Marcellus fonts (SIL Open Font License). To get bloom on gold and fire, filmic colour and soft shadows, the project switched from the built-in renderer to the Universal Render Pipeline (bundled with Unity 6, so no extra download). The 2D view doesn't have its own art: at startup it renders its sprites from the same 3D models, so the two views always match.

## Consequences

- Materials must be assets (`Resources/Palace/Materials`, made by `PalaceSetup` before every build), not created in code, or the build strips the shader variants they need. Runtime tints are copies of those assets.
- The SRP Batcher is off: with it on, objects were drawn with other objects' materials in testing (Unity 6.0.84, URP 17.0.4). Arena blocks are merged with static batching instead. Revisit this after a Unity update.
- Ambient light and reflections are set in code (a warm probe and a generated palace-hall cubemap), not baked, because the arena is built at runtime.
- Swapping in real models later means replacing `PalaceArt`'s builders; the views and the core don't change.
- Arena themes (Fortress, Garden, Frozen, added later beside the palace; `ArenaTheme`) reuse this pipeline: their own CC0 textures and lighting, block models chosen by theme in `PalaceArt`, and 2D sprites rendered per theme with the theme's key as a prefix. Characters, bombs and power-ups are shared.
- The textures and fonts add about 2 MB (about 4 MB with the themes). Their licences (CC0 for the textures, OFL for the fonts, whose text ships beside them) allow use in a paid app.
