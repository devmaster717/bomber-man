namespace BombArena.Core
{
    /// <summary>
    /// One stage attempt's game state, advanced one fixed tick at a time. The view reads this state and
    /// never changes it; all rules live here.
    /// </summary>
    public sealed class Game
    {
        public Arena Arena { get; }
        public Bomber Bomber { get; }

        /// <summary>Ticks run so far.</summary>
        public long Tick { get; private set; }

        public Game(Arena arena, TilePos spawn)
        {
            Arena = arena;
            Bomber = new Bomber(spawn);
        }

        /// <summary>A single-player game on a freshly generated arena.</summary>
        public static Game Create(int width, int height, ulong seed, int softBlockPercent = Arena.DefaultSoftBlockPercent)
        {
            var arena = Arena.Generate(width, height, seed, playerCount: 1, softBlockPercent);
            return new Game(arena, Arena.SpawnTiles(width, height, 1)[0]);
        }

        /// <summary>Advances the game by one tick with the player's held direction.</summary>
        public void Step(Direction input)
        {
            Bomber.Move(input, Arena);
            Tick++;
        }
    }
}
