using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// The flat 2D view: the arena drawn from above with sprites rendered from the 3D models (<see cref="PalaceSprites"/>),
/// under an overhead camera. Holds no rules: every frame it mirrors what the core says. World space: one unit per
/// tile, tile (x, y) centred at (x, -y) so row 0 is at the top.
/// </summary>
public sealed class ArenaRenderer : IArenaView
{
    private const int FloorOrder = 0, BlockOrder = 1, ExitOrder = 2, PowerUpOrder = 3, FireOrder = 5, BombOrder = 6, EnemyOrder = 9, BomberOrder = 10;

    private readonly Game _game;
    private readonly Transform _root;
    private readonly Camera _camera;
    private readonly SpriteRenderer[] _blocks;
    private readonly Tile[] _drawnTiles;
    private readonly ParticleSystem[] _flames;
    private readonly bool[] _burning;
    private readonly List<ParticleSystem> _bursts = new List<ParticleSystem>();
    private readonly List<SpriteRenderer> _bombPool = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _powerUpPool = new List<SpriteRenderer>();
    private readonly SpriteRenderer[] _bombers;
    private readonly SpriteRenderer _exit;
    private readonly Vector2[] _previous, _current;
    private readonly float[] _walkPhase;
    private readonly Vector2[] _ground;
    private readonly List<SpriteRenderer> _enemies = new List<SpriteRenderer>();
    private readonly List<Vector2> _enemyPrevious = new List<Vector2>(), _enemyCurrent = new List<Vector2>();

    /// <summary>Bombers are drawn by player slot; avatars are headshots for menus only.</summary>
    public ArenaRenderer(Game game)
    {
        _game = game;
        _root = new GameObject("Arena").transform;
        var arena = game.Arena;
        int n = arena.Width * arena.Height;
        _blocks = new SpriteRenderer[n];
        _drawnTiles = new Tile[n];
        _flames = new ParticleSystem[n];
        _burning = new bool[n];
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            int i = y * arena.Width + x;
            Make($"Floor ({x},{y})", PalaceSprites.Floor((x + y) % 2 == 1), FloorOrder, new Vector2(x, -y));
            _blocks[i] = Make($"Block ({x},{y})", null, BlockOrder, new Vector2(x, -y));
            ShowTile(x, y, arena[x, y], burst: false);
        }

        _exit = Make("Exit", PalaceSprites.Exit(false), ExitOrder,
            game.ExitTile is TilePos e ? new Vector2(e.X, -e.Y) : Vector2.zero);
        _exit.enabled = false;

        _bombers = new SpriteRenderer[game.Bombers.Count];
        _previous = new Vector2[game.Bombers.Count];
        _current = new Vector2[game.Bombers.Count];
        _walkPhase = new float[game.Bombers.Count];
        _ground = new Vector2[game.Bombers.Count];
        for (int i = 0; i < _bombers.Length; i++)
        {
            _bombers[i] = Make($"Bomber {i}", PalaceSprites.Bomber(i), BomberOrder, Vector2.zero);
            _previous[i] = _current[i] = _ground[i] = WorldPosition(game.Bombers[i]);
        }
        SyncEnemyList();
        _camera = ArenaCamera.SetUp(arena);
        _camera.backgroundColor = ArenaTheme.Current.Backdrop;
        PalaceArt.Finish(_root, _camera, threeD: false);
    }

    public void Follow(int bomber) => ArenaCamera.Follow(_camera, _game.Arena, BomberDrawPosition(bomber));

    public static Vector2 WorldPosition(Bomber b) => World(b.X, b.Y);
    private static Vector2 World(int x, int y) => new Vector2(x / (float)Units.PerTile, -y / (float)Units.PerTile);

    public void OnTick()
    {
        for (int i = 0; i < _bombers.Length; i++)
        {
            _previous[i] = _current[i];
            _current[i] = WorldPosition(_game.Bombers[i]);
        }
        SyncEnemyList();
        for (int i = 0; i < _game.Enemies.Count; i++)
        {
            _enemyPrevious[i] = _enemyCurrent[i];
            _enemyCurrent[i] = World(_game.Enemies[i].X, _game.Enemies[i].Y);
        }
    }

    // Enemies can be added during play (Runners released from the exit).
    private void SyncEnemyList()
    {
        for (int i = _enemies.Count; i < _game.Enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            _enemies.Add(Make($"{e.Kind} {i}", PalaceSprites.Enemy(e.Kind), EnemyOrder, World(e.X, e.Y)));
            _enemyPrevious.Add(World(e.X, e.Y));
            _enemyCurrent.Add(World(e.X, e.Y));
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
                    // Flames rise up the screen from the lower part of the tile.
                    _flames[i] = PalaceArt.Flames(_root, Vector3.up);
                    _flames[i].transform.position = new Vector3(x, -y - 0.35f, -0.5f);
                    foreach (var layer in _flames[i].GetComponentsInChildren<ParticleSystemRenderer>())
                        layer.sortingOrder = layer.name == "Smoke" ? FireOrder - 1 : FireOrder;
                }
                _flames[i].Play();
            }
            else if (_flames[i] != null)
            {
                PalaceArt.Extinguish(_flames[i]);
            }
        }

        _exit.enabled = _game.ExitRevealed;
        if (_game.ExitRevealed)
        {
            _exit.sprite = PalaceSprites.Exit(_game.ExitOpen);
            float glow = _game.ExitOpen ? 1f + 0.06f * Mathf.Sin(time * 4f) : 1f;
            _exit.transform.localScale = new Vector3(glow, glow, 1f);
        }

        int b = 0;
        foreach (var bomb in _game.Bombs)
        {
            if (b == _bombPool.Count) _bombPool.Add(Make("Bomb", PalaceSprites.Bomb(false), BombOrder, Vector2.zero));
            var r = _bombPool[b++];
            r.enabled = true;
            r.sprite = PalaceSprites.Bomb(bomb.IsRemote);
            r.transform.position = Depth(new Vector2(bomb.Tile.X, -bomb.Tile.Y));
            // Pulse as the fuse burns down; remote bombs sit still.
            float pulse = bomb.FuseLeft is int left ? 1f + 0.07f * Mathf.Sin(left * 0.9f) : 1f;
            r.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        for (; b < _bombPool.Count; b++) _bombPool[b].enabled = false;

        int p = 0;
        foreach (var powerUp in _game.PowerUps)
        {
            if (!powerUp.Revealed || powerUp.Kind is not PowerUpKind kind) continue;
            if (p == _powerUpPool.Count) _powerUpPool.Add(Make("Power-up", PalaceSprites.PowerUp(kind), PowerUpOrder, Vector2.zero));
            var r = _powerUpPool[p++];
            r.enabled = true;
            r.sprite = PalaceSprites.PowerUp(kind);
            float bob = 0.04f * Mathf.Sin(time * 2.4f + powerUp.Tile.X * 0.7f + powerUp.Tile.Y);
            r.transform.position = new Vector2(powerUp.Tile.X, -powerUp.Tile.Y + bob);
        }
        for (; p < _powerUpPool.Count; p++) _powerUpPool[p].enabled = false;

        SyncEnemyList();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            _enemies[i].enabled = e.Alive && e.Opacity > 0f;
            _enemies[i].color = new Color(1f, 1f, 1f, e.Opacity);
            float hop = e.Kind == EnemyKind.Runner ? Mathf.Abs(Mathf.Sin(time * 14f + i)) * 0.06f : Mathf.Sin(time * 5f + i) * 0.02f;
            _enemies[i].transform.position = Depth(Vector2.Lerp(_enemyPrevious[i], _enemyCurrent[i], t) + Vector2.up * hop);
        }

        for (int i = 0; i < _bombers.Length; i++)
        {
            var bomber = _game.Bombers[i];
            _bombers[i].enabled = bomber.Alive;
            var now = Vector2.Lerp(_previous[i], _current[i], t);
            float moved = (now - _ground[i]).magnitude;
            _ground[i] = now;
            _walkPhase[i] += moved * 9f;
            // A little bounce in each step.
            float bounce = moved > 1e-4f && moved < 0.5f ? Mathf.Abs(Mathf.Sin(_walkPhase[i])) * 0.05f : 0f;
            _bombers[i].transform.position = Depth(now + Vector2.up * bounce);
        }
    }

    // Characters further down the screen are nearer the viewer, so they are drawn over those above them.
    private static Vector3 Depth(Vector2 p) => new Vector3(p.x, p.y, p.y * 0.01f);

    private void ShowTile(int x, int y, Tile tile, bool burst)
    {
        int i = y * _game.Arena.Width + x;
        var was = _drawnTiles[i];
        _drawnTiles[i] = tile;
        var arena = _game.Arena;
        bool outer = x == 0 || y == 0 || x == arena.Width - 1 || y == arena.Height - 1;
        _blocks[i].sprite = tile switch
        {
            Tile.HardBlock => outer ? PalaceSprites.Wall : PalaceSprites.Pillar,
            Tile.SoftBlock => PalaceSprites.Crate,
            _ => null,
        };
        _blocks[i].enabled = _blocks[i].sprite != null;

        // A breakable block going scatters sparkles.
        if (burst && was == Tile.SoftBlock && tile != Tile.SoftBlock)
        {
            var ps = _bursts.Find(q => !q.IsAlive());
            if (ps == null)
            {
                _bursts.Add(ps = PalaceArt.Burst(_root, Vector3.back, ArenaTheme.Current.Burst));
                ps.GetComponent<ParticleSystemRenderer>().sortingOrder = FireOrder;
            }
            ps.transform.position = new Vector3(x, -y, -0.5f);
            ps.Play();
        }
    }

    // Where the bomber stands, without its walking bounce, so the camera doesn't bob.
    public Vector2 BomberDrawPosition(int index) => _ground[index];

    public void Destroy() => PalaceArt.Remove(_root.gameObject);

    private SpriteRenderer Make(string name, Sprite sprite, int order, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = position;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        return r;
    }
}
