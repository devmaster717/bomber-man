using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>Special enemy rules (spec section 6): the Phantom's teleport cycle and the Wall-passer's soft-block walking.</summary>
    public sealed partial class Game
    {
        /// <summary>A Phantom's teleport destination is at least this many tiles (along the grid) from every bomber.</summary>
        public const int PhantomMinDistanceFromBomber = 3;

        /// <summary>A Phantom stays fully vanished this long between fading out and fading in.</summary>
        public const int PhantomVanishedTicks = Units.TicksPerSecond / 4;

        // Every ~5 s, randomised a little (4–6 s); each fade takes 0.5–1 s.
        private int NextTeleportInterval() => 4 * Units.TicksPerSecond + _rng.Next(2 * Units.TicksPerSecond + 1);
        private int NextFadeLength() => Units.TicksPerSecond / 2 + _rng.Next(Units.TicksPerSecond / 2 + 1);

        /// <summary>
        /// Whether an enemy may enter the tile: open floor (Wall-passers also soft blocks) with no bomb and no fire.
        /// Enemies never walk into fire, so a blast only kills the enemies inside its range when it goes off.
        /// </summary>
        public bool IsOpenForEnemy(Enemy enemy, int x, int y)
        {
            if (!Arena.InBounds(x, y)) return false;
            var tile = Arena[x, y];
            bool passable = tile == Tile.Floor || (tile == Tile.SoftBlock && enemy.Kind == EnemyKind.WallPasser);
            return passable && BombAt(new TilePos(x, y)) == null && !IsBurning(x, y);
        }

        /// <summary>
        /// Advances a Phantom's cycle by one tick. Returns true when it stands still this tick (fading or vanished).
        /// </summary>
        private bool AdvancePhantom(Enemy p)
        {
            switch (p.Phase)
            {
                case PhantomPhase.Visible:
                    if (--p.TeleportCountdown > 0) return false;
                    StartPhase(p, PhantomPhase.FadingOut, NextFadeLength());
                    return true;

                case PhantomPhase.FadingOut:
                    if (--p.PhaseTicksLeft <= 0) StartPhase(p, PhantomPhase.Vanished, PhantomVanishedTicks);
                    return true;

                case PhantomPhase.Vanished:
                    if (--p.PhaseTicksLeft <= 0)
                    {
                        if (ChoosePhantomDestination(p) is TilePos dest)
                        {
                            p.X = dest.X * Units.PerTile;
                            p.Y = dest.Y * Units.PerTile;
                        }
                        p.Heading = Direction.None;
                        StartPhase(p, PhantomPhase.FadingIn, NextFadeLength());
                    }
                    return true;

                default: // FadingIn
                    if (--p.PhaseTicksLeft <= 0)
                    {
                        p.Phase = PhantomPhase.Visible;
                        p.TeleportCountdown = NextTeleportInterval();
                    }
                    return true;
            }
        }

        private static void StartPhase(Enemy p, PhantomPhase phase, int ticks)
        {
            p.Phase = phase;
            p.PhaseTicksLeft = ticks;
            p.PhaseLength = ticks;
        }

        /// <summary>
        /// A random empty floor tile: no bomb, fire, power-up, exit, bomber or enemy, and at least
        /// <see cref="PhantomMinDistanceFromBomber"/> from every bomber. Null if there is none (it reappears where it was).
        /// </summary>
        private TilePos? ChoosePhantomDestination(Enemy phantom)
        {
            var options = new List<TilePos>();
            for (int y = 0; y < Arena.Height; y++)
            for (int x = 0; x < Arena.Width; x++)
            {
                var t = new TilePos(x, y);
                if (IsEmptyFloor(t, phantom)) options.Add(t);
            }
            return options.Count == 0 ? (TilePos?)null : options[_rng.Next(options.Count)];
        }

        /// <summary>Floor with nothing on it, far enough from every living bomber.</summary>
        internal bool IsEmptyFloor(TilePos t, Enemy except = null)
        {
            if (Arena[t] != Tile.Floor || IsBurning(t) || BombAt(t) != null) return false;
            if (ExitRevealed && ExitTile == t) return false;
            if (HasPowerUpAt(t)) return false;
            foreach (var b in _bombers)
                if (b.Alive && Math.Abs(b.Tile.X - t.X) + Math.Abs(b.Tile.Y - t.Y) < PhantomMinDistanceFromBomber) return false;
            foreach (var e in _enemies)
                if (e != except && e.Alive && e.Tile == t) return false;
            return true;
        }

        /// <summary>
        /// Whether fire touches an enemy. A Wall-passer inside a soft block is immune to the blast that hits that
        /// block, so fire that came from a destroyed soft block does not count for it.
        /// </summary>
        private bool FireTouchesEnemy(Enemy e)
        {
            if (e.Kind != EnemyKind.WallPasser) return TouchesFire(e.Tile, e.HitboxOverlaps);
            var c = e.Tile;
            if (Arena[c] == Tile.SoftBlock) return false;
            for (int y = c.Y - 1; y <= c.Y + 1; y++)
            for (int x = c.X - 1; x <= c.X + 1; x++)
            {
                if (!Arena.InBounds(x, y) || !IsBurning(x, y)) continue;
                var t = new TilePos(x, y);
                if (_fireFromSoftBlock[y * Arena.Width + x] && t == c) continue;
                if (e.HitboxOverlaps(t)) return true;
            }
            return false;
        }
    }
}
