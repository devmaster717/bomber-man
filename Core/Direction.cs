namespace BombArena.Core
{
    /// <summary>A movement direction. Row 0 is the top of the arena, so Up decreases y.</summary>
    public enum Direction
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    public static class DirectionExtensions
    {
        public static int Dx(this Direction direction) =>
            direction == Direction.Left ? -1 : direction == Direction.Right ? 1 : 0;

        public static int Dy(this Direction direction) =>
            direction == Direction.Up ? -1 : direction == Direction.Down ? 1 : 0;
    }
}
