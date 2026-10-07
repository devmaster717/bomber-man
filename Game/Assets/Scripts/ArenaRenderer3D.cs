using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The 3D view: the grid game drawn in the current <see cref="ArenaTheme"/> (see <see cref="PalaceArt"/>) under a
/// tilted perspective camera that follows a bomber. Holds no rules: every frame it mirrors what the core says.
/// World space: one unit per tile on the ground (y = 0), tile (x, y) centred at (x, 0, -y) so row 0 is the far side.
/// </summary>
public sealed class ArenaRenderer3D : IArenaView
{
    // Camera: tilted 62° down, high enough to show about 11 rows, like the 2D view.
    private const float Pitch = 62f, FieldOfView = 40f, Height = 11f;

    // Bombers are drawn a bit larger than their 0.6-tile hitbox so they read well among the blocks.
    private const float BomberScale = 1.25f;

    private readonly Game _game;
    private readonly Transform _root;
    private readonly Camera _camera;
    private readonly List<Material> _ownMaterials = new List<Material>();

    private readonly Transform[] _blocks;
    private readonly Transform _blockRoot;
    private readonly HashSet<Transform> _batched = new HashSet<Transform>();
    private readonly Tile[] _drawnTiles;
    private readonly ParticleSystem[] _flames;
    private readonly bool[] _burning;
    private readonly List<ParticleSystem> _bursts = new List<ParticleSystem>();

    private readonly Transform _exit, _exitGlow;
    private readonly Renderer _exitInner;
    private bool? _exitOpenDrawn;

    private readonly List<Transform> _bombPool = new List<Transform>();
    private readonly List<Renderer> _bombBodies = new List<Renderer>();
    private readonly HashSet<TilePos> _bombTiles = new HashSet<TilePos>(), _bombTilesNow = new HashSet<TilePos>();
    private readonly List<(Light light, float age)> _flashes = new List<(Light, float)>();

    private readonly Dictionary<PowerUpKind, List<Transform>> _powerUps = new Dictionary<PowerUpKind, List<Transform>>();

    private readonly PalaceArt.BomberRig[] _bombers;
    private readonly Vector3[] _previous, _current;
    private readonly List<PalaceArt.EnemyRig> _enemies = new List<PalaceArt.EnemyRig>();
    private readonly List<Vector3> _enemyPrevious = new List<Vector3>(), _enemyCurrent = new List<Vector3>();

    public ArenaRenderer3D(Game game)
    {
        _game = game;
        _root = new GameObject("Arena 3D").transform;
        var arena = game.Arena;
        int w = arena.Width, h = arena.Height;
        var centre = new Vector3((w - 1) / 2f, 0f, -(h - 1) / 2f);

        // The floor, as a checker of the theme's two tints (the palace's marble is checkered already).
        var theme = ArenaTheme.Current;
        foreach (bool dark in new[] { false, true })
            PalaceArt.Part(_root, FloorMesh(w, h, dark, theme.FloorTiles),
                PalaceArt.Tint(theme.Floor, dark ? theme.DarkTint : theme.LightTint), Vector3.zero, Vector3.one, false);

        // The theme's surroundings all around (the palace's red carpet, a fortress's earth, ...), and a border of its
        // trim along the arena's edge.
        var groundMaterial = Own(new Material(PalaceArt.Mat(theme.Ground)) { mainTextureScale = new Vector2((w + 40) / theme.GroundTiles, (h + 40) / theme.GroundTiles) });
        var ground = PalaceArt.Part(_root, PalaceArt.Primitive(PrimitiveType.Quad), groundMaterial, centre + Vector3.down * 0.01f, new Vector3(w + 40f, h + 40f, 1f), false);
        ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var gold = PalaceArt.Trim();
        PalaceArt.Part(_root, PalaceArt.ChamferBox(new Vector3(w + 0.3f, 0.05f, 0.15f), 0.02f), gold, new Vector3(centre.x, 0.025f, 0.575f), Vector3.one, false);
        PalaceArt.Part(_root, PalaceArt.ChamferBox(new Vector3(w + 0.3f, 0.05f, 0.15f), 0.02f), gold, new Vector3(centre.x, 0.025f, -(h - 1) - 0.575f), Vector3.one, false);
        PalaceArt.Part(_root, PalaceArt.ChamferBox(new Vector3(0.15f, 0.05f, h + 0.3f), 0.02f), gold, new Vector3(-0.575f, 0.025f, centre.z), Vector3.one, false);
        PalaceArt.Part(_root, PalaceArt.ChamferBox(new Vector3(0.15f, 0.05f, h + 0.3f), 0.02f), gold, new Vector3(w - 1 + 0.575f, 0.025f, centre.z), Vector3.one, false);

        _blocks = new Transform[w * h];
        _drawnTiles = new Tile[w * h];
        _flames = new ParticleSystem[w * h];
        _burning = new bool[w * h];
        // All blocks are merged into a few big meshes (static batching): hundreds of pieces, few draw calls.
        // A crate that breaks is switched off, which batching allows.
        _blockRoot = new GameObject("Blocks").transform;
        _blockRoot.SetParent(_root, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            ShowTile(x, y, arena[x, y], burst: false);
        foreach (var block in _blocks)
            if (block != null) _batched.Add(block);
        StaticBatchingUtility.Combine(_blockRoot.gameObject);

        _exit = PalaceArt.Exit(_root, out _exitInner);
        if (game.ExitTile is TilePos e) _exit.position = new Vector3(e.X, 0f, -e.Y);
        _exitGlow = PalaceArt.Part(_exit, PalaceArt.Primitive(PrimitiveType.Quad), PalaceArt.Tint("Ring", new Color(0.6f, 1.6f, 2.2f)),
            new Vector3(0, 0.05f, 0), Vector3.one * 1.3f, false).transform;
        _exitGlow.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _exit.gameObject.SetActive(false);

        _bombers = new PalaceArt.BomberRig[game.Bombers.Count];
        _previous = new Vector3[_bombers.Length];
        _current = new Vector3[_bombers.Length];
        for (int i = 0; i < _bombers.Length; i++)
        {
            _bombers[i] = PalaceArt.Bomber(_root, i);
            _bombers[i].Root.localScale = Vector3.one * BomberScale;
            _previous[i] = _current[i] = World(game.Bombers[i].X, game.Bombers[i].Y);
            _bombers[i].Root.SetPositionAndRotation(_current[i], Quaternion.Euler(0f, 180f, 0f)); // facing the camera
        }
        SyncEnemyList();

        _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        _camera.orthographic = false;
        _camera.fieldOfView = FieldOfView;
        _camera.nearClipPlane = 0.3f;
        _camera.farClipPlane = 100f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = theme.Backdrop;

        // Reflections come from the theme's generated surroundings (PalaceArt.Surroundings). A realtime probe of the arena was
        // tried: on phones it rendered the empty space above the arena, turning upward-facing gold dark red.
        PalaceArt.Light();
        PalaceArt.Finish(_root, _camera, threeD: true);
    }

    /// <summary>
    /// One quad per tile for the light (or dark) squares of the checker, with the texture mapped across the floor so
    /// one copy spans <paramref name="tilesPerCopy"/> tiles and tile edges meet the texture's own squares.
    /// </summary>
    private static Mesh FloorMesh(int w, int h, bool dark, int tilesPerCopy)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (((x + y) % 2 == 1) != dark) continue;
            int start = verts.Count;
            foreach (var (dx, dz) in new[] { (-0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f), (0.5f, -0.5f) })
            {
                var v = new Vector3(x + dx, 0f, -y + dz);
                verts.Add(v);
                uvs.Add(new Vector2(v.x + 0.5f, v.z + 0.5f) / tilesPerCopy);
            }
            tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = dark ? "Floor dark" : "Floor light", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Material Own(Material m)
    {
        _ownMaterials.Add(m);
        return m;
    }

    private static Vector3 World(int x, int y) => new Vector3(x / (float)Units.PerTile, 0f, -y / (float)Units.PerTile);

    public void OnTick()
    {
        for (int i = 0; i < _bombers.Length; i++)
        {
            _previous[i] = _current[i];
            _current[i] = World(_game.Bombers[i].X, _game.Bombers[i].Y);
        }
        SyncEnemyList();
        for (int i = 0; i < _enemies.Count; i++)
        {
            _enemyPrevious[i] = _enemyCurrent[i];
            _enemyCurrent[i] = World(_game.Enemies[i].X, _game.Enemies[i].Y);
        }
    }

    public void Draw(float t)
    {
        float time = Time.time;
        var arena = _game.Arena;
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            int i = y * arena.Width + x;
            if (_drawnTiles[i] != arena[x, y]) ShowTile(x, y, arena[x, y], burst: true);
            bool burning = _game.IsBurning(x, y);
            if (burning == _burning[i]) continue;
            _burning[i] = burning;
            if (burning)
            {
                if (_flames[i] == null)
                {
                    _flames[i] = PalaceArt.Flames(_root, Vector3.up);
                    _flames[i].transform.position = new Vector3(x, 0.05f, -y);
                }
                _flames[i].Play();
            }
            else if (_flames[i] != null)
            {
                _flames[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        DrawBombs();
        DrawFlashes();

        _exit.gameObject.SetActive(_game.ExitRevealed);
        if (_game.ExitRevealed)
        {
            if (_exitOpenDrawn != _game.ExitOpen)
            {
                _exitOpenDrawn = _game.ExitOpen;
                _exitInner.sharedMaterial = PalaceArt.ExitInner(_game.ExitOpen);
                _exitGlow.gameObject.SetActive(_game.ExitOpen);
            }
            if (_game.ExitOpen) _exitGlow.localScale = Vector3.one * (1.2f + 0.12f * Mathf.Sin(time * 4f));
        }

        DrawPowerUps(time);

        SyncEnemyList();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            var rig = _enemies[i];
            bool shown = e.Alive && e.Opacity > 0f;
            rig.Root.gameObject.SetActive(shown);
            if (!shown) continue;
            rig.Root.position = Vector3.Lerp(_enemyPrevious[i], _enemyCurrent[i], t);
            rig.Pose(time);
            if (rig.Fading.Count > 0) rig.SetOpacity(e.Opacity);
        }

        for (int i = 0; i < _bombers.Length; i++)
        {
            var rig = _bombers[i];
            rig.Root.gameObject.SetActive(_game.Bombers[i].Alive);
            var before = rig.Root.position;
            var now = Vector3.Lerp(_previous[i], _current[i], t);
            rig.Root.position = now;
            // Turn smoothly to face the way the bomber is walking.
            var step = _current[i] - _previous[i];
            if (step.sqrMagnitude > 1e-6f)
                rig.Root.rotation = Quaternion.Slerp(rig.Root.rotation, Quaternion.LookRotation(step, Vector3.up), 0.35f);
            rig.Pose((now - before).magnitude, time);
        }
    }

    private void DrawBombs()
    {
        _bombTilesNow.Clear();
        int b = 0;
        foreach (var bomb in _game.Bombs)
        {
            if (b == _bombPool.Count)
            {
                _bombPool.Add(PalaceArt.Bomb(_root, out var body));
                _bombBodies.Add(body);
            }
            var model = _bombPool[b];
            model.gameObject.SetActive(true);
            // Pulse as the fuse burns down; remote bombs are dark red and still.
            float pulse = bomb.FuseLeft is int left ? 1f + 0.07f * Mathf.Sin(left * 0.9f) : 1f;
            model.position = new Vector3(bomb.Tile.X, 0f, -bomb.Tile.Y);
            model.localScale = Vector3.one * pulse;
            _bombBodies[b].sharedMaterial = PalaceArt.BombMaterial(bomb.IsRemote);
            _bombTilesNow.Add(bomb.Tile);
            b++;
        }
        for (; b < _bombPool.Count; b++) _bombPool[b].gameObject.SetActive(false);

        // A bomb that has just gone off lights up its surroundings for a moment.
        foreach (var tile in _bombTiles)
            if (!_bombTilesNow.Contains(tile) && _game.IsBurning(tile)) Flash(tile);
        _bombTiles.Clear();
        _bombTiles.UnionWith(_bombTilesNow);
    }

    private void Flash(TilePos tile)
    {
        int i = _flashes.FindIndex(f => f.age >= 1f);
        Light light;
        if (i < 0)
        {
            light = new GameObject("Blast light").AddComponent<Light>();
            light.transform.SetParent(_root, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 4.5f;
            light.shadows = LightShadows.None;
            _flashes.Add((light, 0f));
            i = _flashes.Count - 1;
        }
        light = _flashes[i].light;
        light.transform.position = new Vector3(tile.X, 0.9f, -tile.Y);
        light.enabled = true;
        _flashes[i] = (light, 0f);
    }

    private void DrawFlashes()
    {
        for (int i = 0; i < _flashes.Count; i++)
        {
            var (light, age) = _flashes[i];
            if (age >= 1f) continue;
            age = Mathf.Min(1f, age + Time.deltaTime / 0.55f);
            light.intensity = 7f * (1f - age) * (1f - age);
            light.enabled = age < 1f;
            _flashes[i] = (light, age);
        }
    }

    private readonly Dictionary<PowerUpKind, int> _powerUpsUsed = new Dictionary<PowerUpKind, int>();

    private void DrawPowerUps(float time)
    {
        var used = _powerUpsUsed;
        used.Clear();
        foreach (var powerUp in _game.PowerUps)
        {
            if (!powerUp.Revealed || powerUp.Kind is not PowerUpKind kind) continue;
            if (!_powerUps.TryGetValue(kind, out var pool)) _powerUps[kind] = pool = new List<Transform>();
            used.TryGetValue(kind, out int n);
            if (n == pool.Count) pool.Add(PalaceArt.PowerUp(_root, kind));
            var model = pool[n];
            used[kind] = n + 1;
            model.gameObject.SetActive(true);
            // Floating gently above the floor, rocking a little.
            float phase = time * 2.4f + powerUp.Tile.X * 0.7f + powerUp.Tile.Y;
            model.SetPositionAndRotation(new Vector3(powerUp.Tile.X, 0.22f + 0.05f * Mathf.Sin(phase), -powerUp.Tile.Y),
                Quaternion.Euler(-12f, 14f * Mathf.Sin(phase * 0.5f), 0f));
        }
        foreach (var (kind, pool) in _powerUps)
        {
            used.TryGetValue(kind, out int n);
            for (int i = n; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
        }
    }

    private void ShowTile(int x, int y, Tile tile, bool burst)
    {
        int i = y * _game.Arena.Width + x;
        var was = _drawnTiles[i];
        _drawnTiles[i] = tile;
        if (_blocks[i] != null)
        {
            if (_batched.Contains(_blocks[i])) _blocks[i].gameObject.SetActive(false);
            else PalaceArt.Remove(_blocks[i].gameObject);
        }
        _blocks[i] = null;
        var arena = _game.Arena;
        bool outer = x == 0 || y == 0 || x == arena.Width - 1 || y == arena.Height - 1;
        _blocks[i] = tile switch
        {
            Tile.HardBlock => outer ? PalaceArt.Wall(_blockRoot) : PalaceArt.Pillar(_blockRoot),
            Tile.SoftBlock => PalaceArt.Crate(_blockRoot),
            _ => null,
        };
        if (_blocks[i] != null) _blocks[i].position = new Vector3(x, 0f, -y);

        // A breakable block going scatters sparkles.
        if (burst && was == Tile.SoftBlock && tile != Tile.SoftBlock)
        {
            var ps = _bursts.Find(p => !p.IsAlive());
            if (ps == null) _bursts.Add(ps = PalaceArt.Burst(_root, Vector3.up, ArenaTheme.Current.Burst));
            ps.transform.position = new Vector3(x, 0.4f, -y);
            ps.Play();
        }
    }

    private void SyncEnemyList()
    {
        for (int i = _enemies.Count; i < _game.Enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            var rig = PalaceArt.Enemy(_root, e.Kind);
            rig.Seed(i);
            var p = World(e.X, e.Y);
            rig.Root.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f, 0f)); // eyes towards the camera
            _enemies.Add(rig);
            _enemyPrevious.Add(p);
            _enemyCurrent.Add(p);
        }
    }

    public void Follow(int bomber)
    {
        // The ground point at the centre of the screen follows the bomber, clamped so the view stays over the arena;
        // an arena smaller than the view is centred.
        float half = FieldOfView / 2f * Mathf.Deg2Rad, pitch = Pitch * Mathf.Deg2Rad;
        float back = Height / Mathf.Tan(pitch);
        float behind = back - Height / Mathf.Tan(pitch + half);
        float ahead = Height / Mathf.Tan(pitch - half) - back;
        float halfW = Mathf.Sqrt(Height * Height + back * back) * Mathf.Tan(half) * _camera.aspect;

        int w = _game.Arena.Width, h = _game.Arena.Height;
        var target = _bombers[bomber].Root.position;
        float x = Clamp(target.x, -0.5f + halfW, w - 0.5f - halfW, (w - 1) / 2f);
        float z = Clamp(target.z, -(h - 0.5f) + behind, 0.5f - ahead, -(h - 1) / 2f);
        _camera.transform.SetPositionAndRotation(new Vector3(x, Height, z - back), Quaternion.Euler(Pitch, 0f, 0f));
    }

    private static float Clamp(float value, float min, float max, float centreIfTooSmall) =>
        min > max ? centreIfTooSmall : Mathf.Clamp(value, min, max);

    public void Destroy()
    {
        PalaceArt.Remove(_root.gameObject);
        foreach (var m in _ownMaterials) PalaceArt.Remove(m);
    }
}
