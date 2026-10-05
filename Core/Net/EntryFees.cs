namespace BombArena.Core.Net
{
    /// <summary>
    /// The jewels riding on a round (spec section 8). Every player, host included, pays the same entry fee when the
    /// round starts; the last bomber standing takes the whole pot; a draw refunds everyone; if the host's
    /// connection is lost nobody gets their fee back. Each phone settles its own wallet from these rules (ADR 0002).
    /// </summary>
    public static class EntryFees
    {
        public const string NotEnoughJewels = "You need at least the entry fee in jewels to join this room.";

        public static long Pot(long fee, int players) => fee * players;

        public static bool CanPay(PlayerInfo player, long fee) => player.Jewels >= fee;

        /// <summary>
        /// What a player gets back when the round ends. <paramref name="winner"/> is null for a draw;
        /// <paramref name="hostLost"/> means the round never finished.
        /// </summary>
        public static long Payout(int? winner, int me, long fee, int players, bool hostLost)
        {
            if (hostLost) return 0;
            if (winner == null) return fee;
            return winner == me ? Pot(fee, players) : 0;
        }
    }
}
