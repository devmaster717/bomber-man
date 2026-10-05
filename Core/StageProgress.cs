using System;

namespace BombArena.Core
{
    /// <summary>What a stage clear paid and changed.</summary>
    public readonly struct ClearReward
    {
        public readonly long ClearJewels;
        public readonly long StarBonus;
        public readonly bool FirstThreeStars;
        public readonly bool UnlockedNext;

        public ClearReward(long clearJewels, long starBonus, bool firstThreeStars, bool unlockedNext)
        {
            ClearJewels = clearJewels;
            StarBonus = starBonus;
            FirstThreeStars = firstThreeStars;
            UnlockedNext = unlockedNext;
        }

        public long Total => ClearJewels + StarBonus;
    }

    /// <summary>
    /// Progress through the 100 stages (spec section 7): unlocking, best stars, jewel rewards, and the power-ups
    /// carried from one clear into the next stage attempt.
    /// </summary>
    public sealed class StageProgress
    {
        public const long FirstThreeStarBonus = 100;
        public const long JewelsPerStar = 20;

        private readonly PlayerProfile _profile;
        private readonly IWallet _wallet;
        private readonly Action _save;

        public StageProgress(PlayerProfile profile, IWallet wallet, Action save)
        {
            _profile = profile;
            _wallet = wallet;
            _save = save ?? (() => { });
        }

        /// <summary>Jewels paid for every clear of stage n: 100 + 10 × (n − 1).</summary>
        public static long ClearJewels(int stage) => 100 + 10L * (stage - 1);

        /// <summary>Stage 1 is always open; stage n + 1 opens once stage n is cleared with any number of stars.</summary>
        public bool IsUnlocked(int stage) => stage == 1 || (stage >= 2 && stage <= StageTable.StageCount && _profile.BestStars.ContainsKey(stage - 1));

        public int BestStars(int stage) => _profile.BestStars.TryGetValue(stage, out var s) ? s : 0;

        /// <summary>The highest stage the player can start.</summary>
        public int HighestUnlocked
        {
            get
            {
                int n = 1;
                while (n < StageTable.StageCount && IsUnlocked(n + 1)) n++;
                return n;
            }
        }

        /// <summary>
        /// Records a clear: pays the clear reward plus the star bonus (100 the first time a stage is cleared
        /// with 3 stars, otherwise 20 per star), keeps the best stars, and keeps the power-ups held at the clear
        /// for the next stage attempt.
        /// </summary>
        public ClearReward RecordClear(int stage, int stars, PowerUpLoadout heldAtClear)
        {
            bool unlocksNext = stage < StageTable.StageCount && !IsUnlocked(stage + 1);
            long clear = ClearJewels(stage);
            bool first3 = stars == 3 && _profile.ThreeStarBonusPaid.Add(stage);
            long bonus = first3 ? FirstThreeStarBonus : JewelsPerStar * stars;

            _profile.BestStars[stage] = Math.Max(BestStars(stage), stars);
            _profile.CarriedPowerUps = heldAtClear;
            _save();
            _wallet.AddJewels(clear + bonus);
            return new ClearReward(clear, bonus, first3, unlocksNext);
        }

        /// <summary>
        /// The power-ups the next stage attempt starts with: those carried from the last clear plus any bought in
        /// the shop. Taking them empties both, since they are now in play (and lost if the attempt fails).
        /// </summary>
        public PowerUpLoadout TakeStartingLoadout()
        {
            var carried = _profile.CarriedPowerUps;
            var bought = _profile.Inventory;
            _profile.CarriedPowerUps = default;
            _profile.Inventory = default;
            _save();
            return new PowerUpLoadout
            {
                FireUp = carried.FireUp || bought.FireUp,
                BombUps = Math.Min(Bomber.BombCap - 1, carried.BombUps + bought.BombUps),
                RemoteControl = carried.RemoteControl || bought.RemoteControl,
                SpeedUpTicksLeft = Math.Max(carried.SpeedUpTicksLeft, bought.SpeedUpTicksLeft),
            };
        }
    }
}
