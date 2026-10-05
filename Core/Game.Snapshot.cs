using System.IO;

namespace BombArena.Core
{
    /// <summary>
    /// The whole game state as bytes, for Bluetooth rounds: the host sends one every tick and guests copy it into
    /// their own <see cref="Game"/> so the same renderer draws it. Guests never run the rules themselves.
    /// </summary>
    public sealed partial class Game
    {
        public void WriteState(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write((byte)Outcome);
            w.Write(Winner ?? -1);

            int n = Arena.Width * Arena.Height;
            for (int i = 0; i < n; i++) w.Write((byte)Arena[i % Arena.Width, i / Arena.Width]);
            for (int i = 0; i < n; i++) w.Write((byte)_fire[i]);

            w.Write((byte)_bombers.Count);
            foreach (var b in _bombers)
            {
                w.Write(b.X);
                w.Write(b.Y);
                w.Write(b.Alive);
                w.Write(b.HasFireUp);
                w.Write((byte)b.BombUps);
                w.Write(b.HasRemoteControl);
                w.Write((short)b.SpeedUpTicksLeft);
            }

            w.Write((short)_bombs.Count);
            foreach (var b in _bombs)
            {
                w.Write((byte)b.Owner.Index);
                w.Write((byte)b.Tile.X);
                w.Write((byte)b.Tile.Y);
                w.Write((byte)b.Range);
                w.Write((short)(b.FuseLeft ?? -1));
                w.Write(b.OwnerStillOn);
            }

            w.Write((short)_enemies.Count);
            foreach (var e in _enemies)
            {
                w.Write((byte)e.Kind);
                w.Write(e.X);
                w.Write(e.Y);
                w.Write(e.Alive);
                w.Write((byte)e.Phase);
                w.Write((short)e.PhaseTicksLeft);
                w.Write((short)e.PhaseLength);
            }

            w.Write((short)_powerUps.Count);
            foreach (var p in _powerUps)
            {
                w.Write((byte)p.Tile.X);
                w.Write((byte)p.Tile.Y);
                w.Write((sbyte)(p.Kind.HasValue ? (int)p.Kind.Value : -1));
                w.Write(p.Revealed);
            }
        }

        /// <summary>Overwrites this game's state with one written by <see cref="WriteState"/> on the host.</summary>
        public void ReadState(BinaryReader r)
        {
            Tick = r.ReadInt64();
            Outcome = (Outcome)r.ReadByte();
            int winner = r.ReadInt32();
            Winner = winner < 0 ? (int?)null : winner;

            int n = Arena.Width * Arena.Height;
            for (int i = 0; i < n; i++) Arena.SetTile(i, (Tile)r.ReadByte());
            for (int i = 0; i < n; i++) _fire[i] = r.ReadByte();

            int bombers = r.ReadByte();
            for (int i = 0; i < bombers; i++)
            {
                var b = _bombers[i];
                b.X = r.ReadInt32();
                b.Y = r.ReadInt32();
                b.Alive = r.ReadBoolean();
                b.HasFireUp = r.ReadBoolean();
                b.BombUps = r.ReadByte();
                b.HasRemoteControl = r.ReadBoolean();
                b.SpeedUpTicksLeft = r.ReadInt16();
            }

            _bombs.Clear();
            int bombs = r.ReadInt16();
            for (int i = 0; i < bombs; i++)
            {
                var owner = _bombers[r.ReadByte()];
                var tile = new TilePos(r.ReadByte(), r.ReadByte());
                int range = r.ReadByte();
                int fuse = r.ReadInt16();
                var bomb = new Bomb(owner, tile, range, fuse < 0 ? (int?)null : fuse) { OwnerStillOn = r.ReadBoolean() };
                _bombs.Add(bomb);
            }

            int enemies = r.ReadInt16();
            for (int i = 0; i < enemies; i++)
            {
                var kind = (EnemyKind)r.ReadByte();
                var e = i < _enemies.Count ? _enemies[i] : AddEnemy(kind, new TilePos(0, 0));
                e.X = r.ReadInt32();
                e.Y = r.ReadInt32();
                e.Alive = r.ReadBoolean();
                e.Phase = (PhantomPhase)r.ReadByte();
                e.PhaseTicksLeft = r.ReadInt16();
                e.PhaseLength = r.ReadInt16();
            }

            _powerUps.Clear();
            int powerUps = r.ReadInt16();
            for (int i = 0; i < powerUps; i++)
            {
                var tile = new TilePos(r.ReadByte(), r.ReadByte());
                int kind = r.ReadSByte();
                _powerUps.Add(new PowerUp(tile, kind < 0 ? (PowerUpKind?)null : (PowerUpKind)kind) { Revealed = r.ReadBoolean() });
            }
        }

        public byte[] SaveState()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms)) WriteState(w);
            return ms.ToArray();
        }

        public void LoadState(byte[] state)
        {
            using var r = new BinaryReader(new MemoryStream(state));
            ReadState(r);
        }
    }
}
