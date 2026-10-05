using System;
using System.Collections.Generic;

namespace BombArena.Core
{
    public enum Tile
    {
        Floor,
        HardBlock,
        SoftBlock,
    }

    /// <summary>
    /// The tile grid a stage or round is played on (spec section 2). Width and height count the outer wall.
    /// </summary>
    public sealed class Arena
    {
        public const int MinWidth = 19;
        public const int MaxWidth = 27;
        public const int MinHeight = 9;
        public const int MaxHeight = 15;
        public const int DefaultWidth = 23;
        public const int DefaultHeight = 11;
        public const int DefaultSoftBlockPercent = 60;

        private readonly Tile[] _tiles;

        public int Width { get; }
        public int Height { get; }

        private Arena(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new Tile[width * height];
        }

        public Tile this[int x, int y] => _tiles[y * Width + x];
        public Tile this[TilePos pos] => this[pos.X, pos.Y];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Whether the tile itself is open floor (bombs and other objects are checked by the game).</summary>
        public bool IsWalkable(int x, int y) => InBounds(x, y) && this[x, y] == Tile.Floor;

        internal void SetTile(int index, Tile tile) => _tiles[index] = tile;

        internal void DestroySoftBlock(TilePos pos)
        {
            if (this[pos] == Tile.SoftBlock)
                _tiles[pos.Y * Width + pos.X] = Tile.Floor;
        }

        public static bool IsValidSize(int width, int height) =>
            width % 2 == 1 && height % 2 == 1 &&
            width >= MinWidth && width <= MaxWidth &&
            height >= MinHeight && height <= MaxHeight;

        /// <summary>The layout rule: border tiles and tiles whose coordinates are both even are hard blocks.</summary>
        public static bool IsHardByLayout(int x, int y, int width, int height) =>
            x == 0 || y == 0 || x == width - 1 || y == height - 1 || (x % 2 == 0 && y % 2 == 0);

        /// <summary>
        /// Spawn tiles for 1–3 players (spec section 4): top-left, bottom-right, centre.
        /// When the exact centre is a pillar (both coordinates even, e.g. 25 × 13) the centre spawn
        /// moves one tile left, onto the nearest open lane.
        /// </summary>
        public static TilePos[] SpawnTiles(int width, int height, int playerCount)
        {
            if (playerCount < 1 || playerCount > 3)
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "1–3 players");

            var all = new[]
            {
                new TilePos(1, 1),
                new TilePos(width - 2, height - 2),
                Centre(width, height),
            };
            var result = new TilePos[playerCount];
            Array.Copy(all, result, playerCount);
            return result;
        }

        private static TilePos Centre(int width, int height)
        {
            int x = (width - 1) / 2;
            int y = (height - 1) / 2;
            if (x % 2 == 0 && y % 2 == 0)
                x -= 1;
            return new TilePos(x, y);
        }

        /// <summary>
        /// Builds an arena from a seed. Hard blocks follow the layout rule; each spawn tile and its open
        /// neighbours stay clear; soft blocks fill <paramref name="softBlockPercent"/> of the remaining
        /// tiles. The same arguments always produce the same arena.
        /// </summary>
        public static Arena Generate(int width, int height, ulong seed, int playerCount = 1,
            int softBlockPercent = DefaultSoftBlockPercent)
        {
            if (!IsValidSize(width, height))
                throw new ArgumentException(
                    $"Arena must be odd-sized, {MinWidth}–{MaxWidth} × {MinHeight}–{MaxHeight}; got {width} × {height}");

            var arena = new Arena(width, height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                arena._tiles[y * width + x] = IsHardByLayout(x, y, width, height) ? Tile.HardBlock : Tile.Floor;

            var keepClear = new HashSet<TilePos>();
            foreach (var spawn in SpawnTiles(width, height, playerCount))
            {
                keepClear.Add(spawn);
                foreach (var (dx, dy) in Neighbours)
                {
                    var n = new TilePos(spawn.X + dx, spawn.Y + dy);
                    if (arena[n] == Tile.Floor)
                        keepClear.Add(n);
                }
            }

            var candidates = new List<int>();
            for (int i = 0; i < arena._tiles.Length; i++)
            {
                var pos = new TilePos(i % width, i / width);
                if (arena._tiles[i] == Tile.Floor && !keepClear.Contains(pos))
                    candidates.Add(i);
            }

            int softCount = (candidates.Count * softBlockPercent + 50) / 100;
            var rng = new Rng(seed);
            for (int i = 0; i < softCount; i++)
            {
                int pick = i + rng.Next(candidates.Count - i);
                (candidates[i], candidates[pick]) = (candidates[pick], candidates[i]);
                arena._tiles[candidates[i]] = Tile.SoftBlock;
            }

            return arena;
        }

        /// <summary>
        /// Builds an arena from text rows, for tests: '#' hard block, '+' soft block, '.' floor.
        /// </summary>
        public static Arena FromRows(params string[] rows)
        {
            var arena = new Arena(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != arena.Width)
                    throw new ArgumentException($"Row {y} has length {rows[y].Length}, expected {arena.Width}");
                for (int x = 0; x < arena.Width; x++)
                {
                    arena._tiles[y * arena.Width + x] = rows[y][x] switch
                    {
                        '#' => Tile.HardBlock,
                        '+' => Tile.SoftBlock,
                        '.' => Tile.Floor,
                        _ => throw new ArgumentException($"Unknown tile '{rows[y][x]}' at ({x}, {y})"),
                    };
                }
            }
            return arena;
        }

        private static readonly (int dx, int dy)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    }
}
