using System.Collections.Generic;
using BombArena.Core;

/// <summary>
/// Plays sounds for what changed in a game since the last tick: bombs placed and exploding, crates breaking,
/// bombers dying and the player picking up power-ups. It works from the game's state rather than its events, so it
/// sounds the same on a Bluetooth guest (which only receives the host's state) as in stage mode or on the host.
/// </summary>
public sealed class GameSounds
{
    private readonly Game _game;
    private readonly int _me;
    private readonly HashSet<TilePos> _bombs = new HashSet<TilePos>();
    private readonly HashSet<TilePos> _bombsNow = new HashSet<TilePos>();
    private readonly bool[] _alive;
    private int _softBlocks;
    private (bool fire, int bombs, bool remote, bool speed) _myPowers;

    /// <param name="me">The index of this phone's bomber.</param>
    public GameSounds(Game game, int me)
    {
        _game = game;
        _me = me;
        _alive = new bool[game.Bombers.Count];
        Remember();
    }

    /// <summary>Call after each tick (or each state received from the host).</summary>
    public void Observe()
    {
        var fx = Feedback.Instance;
        if (fx == null) return;

        _bombsNow.Clear();
        foreach (var bomb in _game.Bombs)
        {
            _bombsNow.Add(bomb.Tile);
            if (!_bombs.Contains(bomb.Tile)) fx.BombPlaced(mine: bomb.Owner.Index == _me);
        }
        var me = _game.Bombers[_me].Tile;
        foreach (var tile in _bombs)
            if (!_bombsNow.Contains(tile))
                fx.Explosion(nearby: System.Math.Abs(tile.X - me.X) + System.Math.Abs(tile.Y - me.Y) <= 3);

        if (CountSoftBlocks() < _softBlocks) fx.CrateBroke();

        for (int i = 0; i < _alive.Length; i++)
            if (_alive[i] && !_game.Bombers[i].Alive) fx.Died(mine: i == _me);

        var powers = MyPowers();
        if (powers.fire && !_myPowers.fire || powers.bombs > _myPowers.bombs || powers.remote && !_myPowers.remote || powers.speed && !_myPowers.speed)
            fx.PowerUp();

        Remember();
    }

    private void Remember()
    {
        _bombs.Clear();
        foreach (var bomb in _game.Bombs) _bombs.Add(bomb.Tile);
        _softBlocks = CountSoftBlocks();
        for (int i = 0; i < _alive.Length; i++) _alive[i] = _game.Bombers[i].Alive;
        _myPowers = MyPowers();
    }

    private (bool fire, int bombs, bool remote, bool speed) MyPowers()
    {
        var b = _game.Bombers[_me];
        return (b.HasFireUp, b.BombUps, b.HasRemoteControl, b.SpeedUpTicksLeft > 0);
    }

    private int CountSoftBlocks()
    {
        int n = 0;
        var arena = _game.Arena;
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
            if (arena[x, y] == Tile.SoftBlock) n++;
        return n;
    }
}
