using System.Collections.Generic;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// Draws a game's state with sprites. Holds no rules: every frame it mirrors what the core says.
/// World space: one unit per tile, tile (x, y) centred at (x, -y) so row 0 is at the top.
/// </summary>
public sealed class ArenaRenderer
{
    private const int TileOrder = 0, ExitOrder = 1, FireOrder = 5, BombOrder = 6, EnemyOrder = 9, BomberOrder = 10;

    private readonly Game _game;
    private readonly Transform _root;
    private readonly SpriteRenderer[] _tiles;
    private readonly Tile[] _drawnTiles;
    private readonly SpriteRenderer[] _fire;
    private readonly List<SpriteRenderer> _bombPool = new List<SpriteRenderer>();
    private readonly SpriteRenderer[] _bombers;
    private readonly SpriteRenderer _exit;
    private readonly Vector2[] _previous, _current;
    private readonly List<SpriteRenderer> _enemies = new List<SpriteRenderer>();
    private readonly List<Vector2> _enemyPrevious = new List<Vector2>(), _enemyCurrent = new List<Vector2>();

    public ArenaRenderer(Game game)
    {
        _game = game;
        _root = new GameObject("Arena").transform;
        var arena = game.Arena;
        int n = arena.Width * arena.Height;
        _tiles = new SpriteRenderer[n];
        _drawnTiles = new Tile[n];
        _fire = new SpriteRenderer[n];
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            int i = y * arena.Width + x;
            _tiles[i] = Make($"Tile ({x},{y})", PlaceholderSprites.For(arena[x, y]), TileOrder, new Vector2(x, -y));
            _drawnTiles[i] = arena[x, y];
            _fire[i] = Make($"Fire ({x},{y})", PlaceholderSprites.Fire, FireOrder, new Vector2(x, -y));
            _fire[i].enabled = false;
        }

        _exit = Make("Exit", PlaceholderSprites.Exit(false), ExitOrder,
            game.ExitTile is TilePos e ? new Vector2(e.X, -e.Y) : Vector2.zero);
        _exit.enabled = false;

        _bombers = new SpriteRenderer[game.Bombers.Count];
        _previous = new Vector2[game.Bombers.Count];
        _current = new Vector2[game.Bombers.Count];
        for (int i = 0; i < _bombers.Length; i++)
        {
            _bombers[i] = Make($"Bomber {i}", PlaceholderSprites.Bomber, BomberOrder, Vector2.zero);
            _previous[i] = _current[i] = WorldPosition(game.Bombers[i]);
        }
        SyncEnemyList();
    }

    public static Vector2 WorldPosition(Bomber b) => World(b.X, b.Y);
    private static Vector2 World(int x, int y) => new Vector2(x / (float)Units.PerTile, -y / (float)Units.PerTile);

    /// <summary>Call after each core tick, so movement can be interpolated between ticks.</summary>
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
            _enemies.Add(Make($"{e.Kind} {i}", PlaceholderSprites.Enemy(e.Kind), EnemyOrder, World(e.X, e.Y)));
            _enemyPrevious.Add(World(e.X, e.Y));
            _enemyCurrent.Add(World(e.X, e.Y));
        }
    }

    /// <summary>Redraws the state; <paramref name="t"/> is the fraction of the way to the next tick.</summary>
    public void Draw(float t)
    {
        var arena = _game.Arena;
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            int i = y * arena.Width + x;
            if (_drawnTiles[i] != arena[x, y])
            {
                _drawnTiles[i] = arena[x, y];
                _tiles[i].sprite = PlaceholderSprites.For(arena[x, y]);
            }
            _fire[i].enabled = _game.IsBurning(x, y);
        }

        _exit.enabled = _game.ExitRevealed;
        if (_game.ExitRevealed) _exit.sprite = PlaceholderSprites.Exit(_game.ExitOpen);

        int b = 0;
        foreach (var bomb in _game.Bombs)
        {
            if (b == _bombPool.Count) _bombPool.Add(Make("Bomb", PlaceholderSprites.Bomb, BombOrder, Vector2.zero));
            var r = _bombPool[b++];
            r.enabled = true;
            r.transform.position = new Vector2(bomb.Tile.X, -bomb.Tile.Y);
            // Pulse as the fuse burns down.
            float pulse = bomb.FuseLeft is int left ? 1f + 0.08f * Mathf.Sin(left * 0.9f) : 1f;
            r.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
        for (; b < _bombPool.Count; b++) _bombPool[b].enabled = false;

        SyncEnemyList();
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _game.Enemies[i];
            _enemies[i].enabled = e.Alive;
            _enemies[i].transform.position = Vector2.Lerp(_enemyPrevious[i], _enemyCurrent[i], t);
        }

        for (int i = 0; i < _bombers.Length; i++)
        {
            var bomber = _game.Bombers[i];
            _bombers[i].enabled = bomber.Alive;
            _bombers[i].transform.position = Vector2.Lerp(_previous[i], _current[i], t);
        }
    }

    public Vector2 BomberDrawPosition(int index) => _bombers[index].transform.position;

    public void Destroy() => Object.Destroy(_root.gameObject);

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
