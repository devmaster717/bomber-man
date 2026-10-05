using System;

namespace BombArena.Core
{
    /// <summary>A tile coordinate: column X, row Y, both counted from 0 at the top-left.</summary>
    public readonly struct TilePos : IEquatable<TilePos>
    {
        public readonly int X;
        public readonly int Y;

        public TilePos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TilePos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is TilePos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(TilePos a, TilePos b) => a.Equals(b);
        public static bool operator !=(TilePos a, TilePos b) => !a.Equals(b);
    }
}
