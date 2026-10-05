using System.Collections.Generic;

namespace BombArena.Core
{
    /// <summary>Power-up rules (spec section 5): hidden or dropped under soft blocks, revealed after the fire, picked up by walking on them.</summary>
    public sealed partial class Game
    {
        private readonly List<PowerUp> _powerUps = new List<PowerUp>();

        public IReadOnlyList<PowerUp> PowerUps => _powerUps;

        /// <summary>
        /// Chance (0–100) that any destroyed soft block reveals a power-up. Bluetooth rounds use 40; stage mode
        /// uses 0 and instead hides exactly one power-up per attempt (<see cref="HidePowerUp"/>).
        /// </summary>
        public int SoftBlockDropPercent { get; set; }

        /// <summary>Raised when a bomber picks up a power-up.</summary>
        public event System.Action<Bomber, PowerUpKind> PowerUpPicked;

        /// <summary>
        /// Hides one power-up under a random soft block other than the exit's. Its type is drawn when it appears,
        /// leaving out types the bomber cannot use at that moment.
        /// </summary>
        public void HidePowerUp(ulong attemptSeed)
        {
            var rng = new Rng(attemptSeed ^ 0x9077_E2A9UL);
            var soft = new List<TilePos>();
            for (int y = 0; y < Arena.Height; y++)
            for (int x = 0; x < Arena.Width; x++)
            {
                var t = new TilePos(x, y);
                if (Arena[t] == Tile.SoftBlock && t != ExitTile) soft.Add(t);
            }
            if (soft.Count > 0)
                _powerUps.Add(new PowerUp(soft[rng.Next(soft.Count)], kind: null));
        }

        /// <summary>Places a power-up directly (for tests and hand-made layouts); revealed if not under a soft block.</summary>
        public PowerUp AddPowerUp(TilePos tile, PowerUpKind? kind)
        {
            var p = new PowerUp(tile, kind) { Revealed = Arena[tile] != Tile.SoftBlock };
            _powerUps.Add(p);
            return p;
        }

        /// <summary>Gives a bomber power-ups at the start: carried over from the last stage or bought in the shop.</summary>
        public void ApplyLoadout(int bomberIndex, PowerUpLoadout loadout) => _bombers[bomberIndex].Apply(loadout);

        private PowerUp PowerUpAt(TilePos t)
        {
            foreach (var p in _powerUps)
                if (p.Tile == t) return p;
            return null;
        }

        private bool HasPowerUpAt(TilePos t) => PowerUpAt(t) is PowerUp p && p.Revealed;

        /// <summary>A soft block was destroyed: uncover a power-up hidden there, or roll for a dropped one.</summary>
        private void OnSoftBlockDestroyed(TilePos t)
        {
            var hidden = PowerUpAt(t);
            if (hidden != null)
            {
                hidden.Uncovering = true;
                return;
            }
            if (SoftBlockDropPercent > 0 && _rng.Next(100) < SoftBlockDropPercent)
                _powerUps.Add(new PowerUp(t, (PowerUpKind)_rng.Next(4)) { Uncovering = true });
        }

        /// <summary>Fire reaching a power-up that is already showing destroys it.</summary>
        private void OnFire(TilePos t)
        {
            var p = PowerUpAt(t);
            if (p != null && p.Revealed) _powerUps.Remove(p);
        }

        /// <summary>Each tick after fire burns down: power-ups whose uncovering fire is out appear.</summary>
        private void UpdatePowerUps()
        {
            foreach (var p in _powerUps)
            {
                if (!p.Uncovering || IsBurning(p.Tile)) continue;
                p.Uncovering = false;
                p.Revealed = true;
                p.Kind ??= DrawUsableKind(Bomber);
            }
        }

        private PowerUpKind DrawUsableKind(Bomber bomber)
        {
            var usable = new List<PowerUpKind>();
            foreach (PowerUpKind k in new[] { PowerUpKind.FireUp, PowerUpKind.BombUp, PowerUpKind.RemoteControl, PowerUpKind.SpeedUp })
                if (bomber.CanUse(k)) usable.Add(k);
            return usable[_rng.Next(usable.Count)];
        }

        private void PickUpPowerUps()
        {
            foreach (var bomber in _bombers)
            {
                if (!bomber.Alive) continue;
                var p = PowerUpAt(bomber.Tile);
                if (p == null || !p.Revealed || p.Kind == null) continue;
                bomber.Apply(p.Kind.Value);
                _powerUps.Remove(p);
                PowerUpPicked?.Invoke(bomber, p.Kind.Value);
            }
        }
    }
}
