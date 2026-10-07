using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// The 3D view: the same grid game drawn with simple placeholder shapes under a tilted perspective camera that
/// follows a bomber. Holds no rules: every frame it mirrors what the core says.
/// World space: one unit per tile on the ground (y = 0), tile (x, y) centred at (x, 0, -y) so row 0 is the far side.
/// </summary>
public sealed class ArenaRenderer3D : IArenaView
{
    // Camera: tilted 62° down, high enough to show about 11 rows, like the 2D view.
    private const float Pitch = 62f, FieldOfView = 40f, Height = 11f;

    // Blocks are a little lower than a tile is wide, so bombers behind them stay visible.
    private const float BlockHeight = 0.8f;

    // Bombers are drawn a bit larger than their 0.6-tile hitbox so they read well among the blocks.
    private const float BomberScale = 1.25f;

    private static readonly Color Background = new Color(0.07f, 0.09f, 0.13f);

    private readonly Game _game;
    private readonly Transform _root;
    private readonly Camera _camera;
    private readonly GameObject[] _blocks, _fire;
    private readonly Tile[] _drawnTiles;
    private readonly GameObject _exit;
    private readonly Renderer _exitRenderer;
    private readonly List<GameObject> _bombPool = new List<GameObject>();
    private readonly List<Renderer> _bombBodies = new List<Renderer>();
    private readonly List<GameObject> _powerUpPool = new List<GameObject>();
    private readonly List<PowerUpKind?> _powerUpKinds = new List<PowerUpKind?>();
    private readonly Material _bombMaterial = Materials.Solid(new Color(0.1f, 0.1f, 0.13f));
    private readonly Material _remoteBombMaterial = Materials.Solid(new Color(0.55f, 0.12f, 0.12f));
    private bool? _exitOpenDrawn;
    private readonly Transform[] _bombers;
    private readonly Vector3[] _previous, _current;
    private readonly List<EnemyModel> _enemies = new List<EnemyModel>();
    private readonly float _savedShadowDistance;

    public ArenaRenderer3D(Game game)
    {
        _game = game;
        _root = new GameObject("Arena 3D").transform;
        var arena = game.Arena;
        int w = arena.Width, h = arena.Height;

        // One floor quad under the whole arena, its checker texture repeated once per tile.
        var floor = Part(PrimitiveType.Quad, _root, Materials.Textured(PlaceholderSprites.For(Tile.Floor).texture, new Vector2(w, h)));
        floor.transform.SetPositionAndRotation(new Vector3((w - 1) / 2f, 0f, -(h - 1) / 2f), Quaternion.Euler(90f, 0f, 0f));
        floor.transform.localScale = new Vector3(w, h, 1f);
        floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // A dark tabletop around the arena, for the far corners the tilted camera can see past the walls.
        var table = Part(PrimitiveType.Quad, _root, Materials.Solid(new Color(0.17f, 0.2f, 0.25f)));
        table.transform.SetPositionAndRotation(new Vector3((w - 1) / 2f, -0.02f, -(h - 1) / 2f), Quaternion.Euler(90f, 0f, 0f));
        table.transform.localScale = new Vector3(w + 60f, h + 60f, 1f);

        _blocks = new GameObject[w * h];
        _fire = new GameObject[w * h];
        _drawnTiles = new Tile[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            _blocks[i] = Part(PrimitiveType.Cube, _root, null);
            _blocks[i].transform.position = new Vector3(x, BlockHeight / 2f, -y);
            _blocks[i].transform.localScale = new Vector3(1f, BlockHeight, 1f);
            ShowTile(i, arena[x, y]);

            // Fire: a low glowing slab filling the tile.
            _fire[i] = Part(PrimitiveType.Cube, _root, Materials.Glowing(PlaceholderSprites.Fire.texture));
            _fire[i].transform.position = new Vector3(x, 0.2f, -y);
            _fire[i].transform.localScale = new Vector3(0.95f, 0.4f, 0.95f);
            _fire[i].GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fire[i].SetActive(false);
        }

        _exit = Part(PrimitiveType.Quad, _root, Materials.Textured(PlaceholderSprites.Exit(false).texture));
        _exitRenderer = _exit.GetComponent<Renderer>();
        _exit.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (game.ExitTile is TilePos e) _exit.transform.position = new Vector3(e.X, 0.01f, -e.Y);
        _exit.SetActive(false);

        _bombers = new Transform[game.Bombers.Count];
        _previous = new Vector3[_bombers.Length];
        _current = new Vector3[_bombers.Length];
        for (int i = 0; i < _bombers.Length; i++)
        {
            _bombers[i] = BomberModel(i);
            _previous[i] = _current[i] = World(game.Bombers[i].X, game.Bombers[i].Y);
            _bombers[i].position = _current[i];
            _bombers[i].rotation = Quaternion.Euler(0f, 180f, 0f); // facing the camera
        }
        SyncEnemyList();

        _camera = Camera.main != null ? Camera.main : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        _camera.orthographic = false;
        _camera.fieldOfView = FieldOfView;
        _camera.nearClipPlane = 0.3f;
        _camera.farClipPlane = 100f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Background;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
        _savedShadowDistance = QualitySettings.shadowDistance;
        QualitySettings.shadowDistance = 30f;
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
            _enemies[i].Previous = _enemies[i].Current;
            _enemies[i].Current = World(_game.Enemies[i].X, _game.Enemies[i].Y);
        }
    }

    public void Draw(float t)
    {
        var arena = _game.Arena;
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            int i = y * arena.Width + x;
            if (_drawnTiles[i] != arena[x, y]) ShowTile(i, arena[x, y]);
            bool burning = _game.IsBurning(x, y);
            _fire[i].SetActive(burning);
            if (burning)
            {
                float flicker = 0.4f + 0.08f * Mathf.Sin(Time.time * 30f + x * 1.7f + y * 2.3f);
                _fire[i].transform.localScale = new Vector3(0.95f, flicker, 0.95f);
                _fire[i].transform.position = new Vector3(x, flicker / 2f, -y);
            }
        }

        _exit.SetActive(_game.ExitRevealed);
        if (_game.ExitRevealed && _exitOpenDrawn != _game.ExitOpen)
        {
            _exitOpenDrawn = _game.ExitOpen;
            _exitRenderer.sharedMaterial = Materials.Textured(PlaceholderSprites.Exit(_game.ExitOpen).texture);
        }

        int b = 0;
        foreach (var bomb in _game.Bombs)
        {
            if (b == _bombPool.Count) AddBomb();
            var go = _bombPool[b];
            go.SetActive(true);
            // Pulse as the fuse burns down; remote bombs sit still with a red tint.
            float pulse = bomb.FuseLeft is int left ? 1f + 0.08f * Mathf.Sin(left * 0.9f) : 1f;
            go.transform.position = new Vector3(bomb.Tile.X, 0f, -bomb.Tile.Y);
            go.transform.localScale = Vector3.one * pulse;
            _bombBodies[b].sharedMaterial = bomb.IsRemote ? _remoteBombMaterial : _bombMaterial;
            b++;
        }
        for (; b < _bombPool.Count; b++) _bombPool[b].SetActive(false);

        int p = 0;
        foreach (var powerUp in _game.PowerUps)
        {
            if (!powerUp.Revealed || powerUp.Kind is not PowerUpKind kind) continue;
            if (p == _powerUpPool.Count)
            {
                var card = Part(PrimitiveType.Cube, _root, null);
                card.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
                _powerUpPool.Add(card);
                _powerUpKinds.Add(null);
            }
            var go = _powerUpPool[p];
            go.SetActive(true);
            if (_powerUpKinds[p] != kind)
            {
                _powerUpKinds[p] = kind;
                go.GetComponent<Renderer>().sharedMaterial = Materials.Textured(PlaceholderSprites.PowerUp(kind).texture);
            }
            p++;
            // A gently bobbing card, picture side up.
            float bob = 0.15f + 0.05f * Mathf.Sin(Time.time * 4f + powerUp.Tile.X);
            go.transform.SetPositionAndRotation(new Vector3(powerUp.Tile.X, bob, -powerUp.Tile.Y), Quaternion.Euler(0f, 180f, 0f));
        }
        for (; p < _powerUpPool.Count; p++) _powerUpPool[p].SetActive(false);

        SyncEnemyList();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            var model = _enemies[i];
            model.Root.gameObject.SetActive(e.Alive && e.Opacity > 0f);
            model.Root.position = Vector3.Lerp(model.Previous, model.Current, t);
            if (model.Fades) model.SetOpacity(e.Opacity);
        }

        for (int i = 0; i < _bombers.Length; i++)
        {
            _bombers[i].gameObject.SetActive(_game.Bombers[i].Alive);
            _bombers[i].position = Vector3.Lerp(_previous[i], _current[i], t);
            // Turn to face the way the bomber is walking.
            var step = _current[i] - _previous[i];
            if (step.sqrMagnitude > 1e-6f) _bombers[i].rotation = Quaternion.LookRotation(step, Vector3.up);
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
        var target = _bombers[bomber].position;
        float x = Clamp(target.x, -0.5f + halfW, w - 0.5f - halfW, (w - 1) / 2f);
        float z = Clamp(target.z, -(h - 0.5f) + behind, 0.5f - ahead, -(h - 1) / 2f);
        _camera.transform.SetPositionAndRotation(new Vector3(x, Height, z - back), Quaternion.Euler(Pitch, 0f, 0f));
    }

    private static float Clamp(float value, float min, float max, float centreIfTooSmall) =>
        min > max ? centreIfTooSmall : Mathf.Clamp(value, min, max);

    public void Destroy()
    {
        QualitySettings.shadowDistance = _savedShadowDistance;
        Remove(_root.gameObject);
    }

    // Destroy outside play mode too, so the editor can render previews (see Previews.cs).
    private static void Remove(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    // ---- models ----

    private void ShowTile(int i, Tile tile)
    {
        _drawnTiles[i] = tile;
        bool solid = tile == Tile.HardBlock || tile == Tile.SoftBlock;
        _blocks[i].SetActive(solid);
        if (solid) _blocks[i].GetComponent<Renderer>().sharedMaterial = Materials.Textured(PlaceholderSprites.For(tile).texture);
    }

    private void AddBomb()
    {
        var root = new GameObject("Bomb").transform;
        root.SetParent(_root, false);
        var body = Part(PrimitiveType.Sphere, root, _bombMaterial);
        body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        body.transform.localScale = Vector3.one * 0.76f;
        var fuse = Part(PrimitiveType.Cylinder, root, Materials.Solid(new Color(0.85f, 0.75f, 0.55f)));
        fuse.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        fuse.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
        var spark = Part(PrimitiveType.Sphere, root, Materials.Glowing(PlaceholderSprites.Fire.texture));
        spark.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        spark.transform.localScale = Vector3.one * 0.14f;
        _bombPool.Add(root.gameObject);
        _bombBodies.Add(body.GetComponent<Renderer>());
    }

    /// <summary>A bomber: shirt-coloured body, round head with eyes, and feet; slot colours match the 2D sprites.</summary>
    private Transform BomberModel(int slot)
    {
        var root = new GameObject($"Bomber {slot}").transform;
        root.SetParent(_root, false);
        root.localScale = Vector3.one * BomberScale;
        var shirt = Materials.Solid(PlaceholderSprites.SlotShirt(slot));
        var feet = Materials.Solid(PlaceholderSprites.SlotFeet(slot));
        var skin = Materials.Solid(new Color(1f, 0.88f, 0.76f));
        var dark = Materials.Solid(new Color(0.1f, 0.1f, 0.1f));

        Place(Part(PrimitiveType.Cylinder, root, shirt), new Vector3(0f, 0.32f, 0f), new Vector3(0.46f, 0.2f, 0.46f));
        Place(Part(PrimitiveType.Sphere, root, skin), new Vector3(0f, 0.72f, 0f), Vector3.one * 0.46f);
        Place(Part(PrimitiveType.Sphere, root, dark), new Vector3(-0.09f, 0.75f, 0.2f), Vector3.one * 0.07f);
        Place(Part(PrimitiveType.Sphere, root, dark), new Vector3(0.09f, 0.75f, 0.2f), Vector3.one * 0.07f);
        Place(Part(PrimitiveType.Sphere, root, shirt), new Vector3(0f, 0.97f, 0f), Vector3.one * 0.1f); // pompom
        Place(Part(PrimitiveType.Cube, root, feet), new Vector3(-0.12f, 0.06f, 0.04f), new Vector3(0.15f, 0.12f, 0.24f));
        Place(Part(PrimitiveType.Cube, root, feet), new Vector3(0.12f, 0.06f, 0.04f), new Vector3(0.15f, 0.12f, 0.24f));
        return root;
    }

    private void SyncEnemyList()
    {
        for (int i = _enemies.Count; i < _game.Enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            var model = new EnemyModel(e.Kind, _root, $"{e.Kind} {i}");
            model.Previous = model.Current = World(e.X, e.Y);
            model.Root.position = model.Current;
            _enemies.Add(model);
        }
    }

    /// <summary>
    /// An enemy: a coloured body with big eyes looking at the camera. Each kind has its own shape so they can be told
    /// apart at a glance: Walker a ball, Runner a tall red bean, Wall-passer a green block, Phantom a see-through ghost.
    /// </summary>
    private sealed class EnemyModel
    {
        public readonly Transform Root;
        public readonly bool Fades;
        public Vector3 Previous, Current;
        private readonly List<(Renderer renderer, Color colour)> _parts = new List<(Renderer, Color)>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        public EnemyModel(EnemyKind kind, Transform parent, string name)
        {
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);
            Fades = kind == EnemyKind.Phantom;
            var colour = kind switch
            {
                EnemyKind.Runner => new Color(0.88f, 0.22f, 0.24f),
                EnemyKind.Phantom => new Color(0.85f, 0.9f, 1f, 0.8f),
                EnemyKind.WallPasser => new Color(0.18f, 0.64f, 0.48f),
                _ => new Color(0.56f, 0.27f, 0.79f),
            };
            var (shape, scale, y) = kind switch
            {
                EnemyKind.Runner => (PrimitiveType.Sphere, new Vector3(0.55f, 0.8f, 0.55f), 0.4f),
                EnemyKind.WallPasser => (PrimitiveType.Cube, new Vector3(0.62f, 0.62f, 0.62f), 0.31f),
                _ => (PrimitiveType.Sphere, Vector3.one * 0.72f, 0.38f),
            };
            Add(shape, colour, new Vector3(0f, y, 0f), scale);
            float eyeY = y + scale.y * 0.15f, eyeZ = -scale.z * 0.45f; // on the side facing the camera
            Add(PrimitiveType.Sphere, Color.white, new Vector3(-0.12f, eyeY, eyeZ), Vector3.one * 0.18f);
            Add(PrimitiveType.Sphere, Color.white, new Vector3(0.12f, eyeY, eyeZ), Vector3.one * 0.18f);
            Add(PrimitiveType.Sphere, new Color(0.07f, 0.07f, 0.07f), new Vector3(-0.12f, eyeY, eyeZ - 0.07f), Vector3.one * 0.08f);
            Add(PrimitiveType.Sphere, new Color(0.07f, 0.07f, 0.07f), new Vector3(0.12f, eyeY, eyeZ - 0.07f), Vector3.one * 0.08f);
        }

        private void Add(PrimitiveType shape, Color colour, Vector3 position, Vector3 scale)
        {
            var part = Part(shape, Root, Fades ? Materials.SeeThrough(colour) : Materials.Solid(colour));
            Place(part, position, scale);
            var r = part.GetComponent<Renderer>();
            if (Fades) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _parts.Add((r, colour));
        }

        /// <summary>The Phantom fades in and out where it stands.</summary>
        public void SetOpacity(float opacity)
        {
            foreach (var (r, colour) in _parts)
            {
                _block.SetColor("_Color", new Color(colour.r, colour.g, colour.b, colour.a * opacity));
                r.SetPropertyBlock(_block);
            }
        }
    }

    private static GameObject Part(PrimitiveType shape, Transform parent, Material material)
    {
        var go = GameObject.CreatePrimitive(shape);
        Remove(go.GetComponent<Collider>()); // drawing only; the core does all collisions
        go.transform.SetParent(parent, false);
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void Place(GameObject go, Vector3 localPosition, Vector3 localScale)
    {
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
    }

    /// <summary>
    /// Shared materials, made once per colour or texture. The shaders are listed as always included in the build
    /// (see Builds.cs), because nothing else in the project references them.
    /// </summary>
    private static class Materials
    {
        public const string LitShader = "Legacy Shaders/Diffuse";
        public const string SeeThroughShader = "Legacy Shaders/Transparent/Diffuse";
        public const string GlowShader = "Unlit/Texture";

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Solid(Color c) => Get("solid" + c, () => new Material(Find(LitShader)) { color = c });

        public static Material SeeThrough(Color c) => Get("clear" + c, () => new Material(Find(SeeThroughShader)) { color = c });

        public static Material Textured(Texture texture, Vector2? tiling = null) =>
            Get("tex" + texture.GetInstanceID() + tiling, () =>
            {
                var m = new Material(Find(LitShader)) { mainTexture = texture };
                if (tiling is Vector2 t)
                {
                    texture.wrapMode = TextureWrapMode.Repeat;
                    m.mainTextureScale = t;
                }
                return m;
            });

        public static Material Glowing(Texture texture) =>
            Get("glow" + texture.GetInstanceID(), () => new Material(Find(GlowShader)) { mainTexture = texture });

        // If a shader was left out of the build, draw unlit rather than fail.
        private static Shader Find(string name) => Shader.Find(name) ?? Shader.Find("Sprites/Default");

        private static Material Get(string key, System.Func<Material> make)
        {
            if (!Cache.TryGetValue(key, out var m) || m == null) Cache[key] = m = make();
            return m;
        }
    }
}
