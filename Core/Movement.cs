using System;

namespace BombArena.Core
{
    /// <summary>
    /// Grid movement shared by bombers (and later enemies): four directions, smooth between tile centres,
    /// stopped by unwalkable tiles, with corner assist (spec section 4).
    /// </summary>
    public static class Movement
    {
        /// <summary>
        /// How far off a lane (in position units) a mover may be and still be nudged onto it when turning:
        /// about 35% of a tile.
        /// </summary>
        public const int CornerAssist = Units.PerTile * 35 / 100;

        /// <summary>
        /// Moves a point by up to <paramref name="budget"/> units in <paramref name="direction"/>.
        /// A mover is a tile-sized box centred on (x, y). It is only ever off-centre on one axis.
        /// </summary>
        public static void Step(ref int x, ref int y, Direction direction, int budget, Func<int, int, bool> walkable)
        {
            if (direction == Direction.None || budget <= 0)
                return;

            if (direction.Dx() != 0)
                StepAlong(ref x, ref y, direction.Dx(), budget, (along, across) => walkable(along, across));
            else
                StepAlong(ref y, ref x, direction.Dy(), budget, (along, across) => walkable(across, along));
        }

        // "along" is the axis of travel, "across" the perpendicular one; walkable takes (alongTile, acrossTile).
        private static void StepAlong(ref int along, ref int across, int sign, int budget, Func<int, int, bool> walkable)
        {
            int lane = Units.NearestTile(across);
            int offset = across - lane * Units.PerTile;

            if (offset != 0)
            {
                // Turning while off a lane: only allowed close to the lane and when the lane ahead is open.
                if (Math.Abs(offset) > CornerAssist)
                    return;
                int alongTile = Units.NearestTile(along);
                if (!walkable(alongTile + sign, lane))
                    return;
                int nudge = Math.Min(budget, Math.Abs(offset));
                across -= Math.Sign(offset) * nudge;
                budget -= nudge;
                if (budget == 0)
                    return;
            }

            // The box spans [c - half, c + half); its leading edge decides which tile it is entering.
            int target = along + sign * budget;
            if (sign > 0)
            {
                int lead = (target + Units.PerTile - 1) / Units.PerTile;
                if (!walkable(lead, lane))
                    target = (lead - 1) * Units.PerTile;
            }
            else
            {
                int lead = target / Units.PerTile;
                if (!walkable(lead, lane))
                    target = (lead + 1) * Units.PerTile;
            }

            // Never move backwards because of a clamp.
            along = sign > 0 ? Math.Max(along, target) : Math.Min(along, target);
        }
    }
}
