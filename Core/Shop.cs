using System;

namespace BombArena.Core
{
    public enum PurchaseResult
    {
        Bought,
        NotEnoughJewels,
        NotForSale,
    }

    /// <summary>
    /// What jewels can buy (spec section 9): the 10-life pack, power-ups for the inventory and avatars. Jewel packs
    /// for real money are a placeholder until the server exists (ADR 0002).
    /// </summary>
    public sealed class Shop
    {
        public const long LifePackPrice = 500;
        public const int LifePackLives = 10;
        public const long AvatarPrice = 1000;

        private readonly PlayerProfile _profile;
        private readonly IWallet _wallet;
        private readonly Action _save;

        public Shop(PlayerProfile profile, IWallet wallet, Action save)
        {
            _profile = profile;
            _wallet = wallet;
            _save = save ?? (() => { });
        }

        public static long PowerUpPrice(PowerUpKind kind) => kind == PowerUpKind.SpeedUp ? 100 : 300;

        /// <summary>Always adds 10 lives, even beyond 10.</summary>
        public PurchaseResult BuyLifePack()
        {
            if (!_wallet.TrySpendJewels(LifePackPrice)) return PurchaseResult.NotEnoughJewels;
            _wallet.AddLives(LifePackLives);
            return PurchaseResult.Bought;
        }

        /// <summary>
        /// Whether the shop sells this power-up now: not a type the player already holds (carried from the last
        /// clear) or has in the inventory, except Speed Up; and not Bomb Up beyond 4 bombs.
        /// </summary>
        public bool CanBuy(PowerUpKind kind)
        {
            var held = _profile.CarriedPowerUps;
            var inv = _profile.Inventory;
            return kind switch
            {
                PowerUpKind.FireUp => !held.FireUp && !inv.FireUp,
                PowerUpKind.RemoteControl => !held.RemoteControl && !inv.RemoteControl,
                PowerUpKind.BombUp => 1 + held.BombUps + inv.BombUps < Bomber.BombCap,
                _ => true,
            };
        }

        /// <summary>Buys a power-up into the inventory; it is applied at the start of the next attempt or round.</summary>
        public PurchaseResult BuyPowerUp(PowerUpKind kind)
        {
            if (!CanBuy(kind)) return PurchaseResult.NotForSale;
            if (!_wallet.TrySpendJewels(PowerUpPrice(kind))) return PurchaseResult.NotEnoughJewels;
            var inv = _profile.Inventory;
            switch (kind)
            {
                case PowerUpKind.FireUp: inv.FireUp = true; break;
                case PowerUpKind.BombUp: inv.BombUps++; break;
                case PowerUpKind.RemoteControl: inv.RemoteControl = true; break;
                case PowerUpKind.SpeedUp: inv.SpeedUpTicksLeft = Bomber.SpeedUpTicks; break;
            }
            _profile.Inventory = inv;
            _save();
            return PurchaseResult.Bought;
        }

        public bool Owns(int avatar) => _profile.OwnedAvatars.Contains(avatar);

        public PurchaseResult BuyAvatar(int avatar)
        {
            if (avatar < 0 || avatar >= PlayerProfile.AvatarCount || Owns(avatar)) return PurchaseResult.NotForSale;
            if (!_wallet.TrySpendJewels(AvatarPrice)) return PurchaseResult.NotEnoughJewels;
            _profile.OwnedAvatars.Add(avatar);
            _save();
            return PurchaseResult.Bought;
        }

        /// <summary>
        /// Placeholder for buying jewels with real money by QR payment: the server and payment provider do not
        /// exist yet, so this credits nothing and says so.
        /// </summary>
        public bool BuyJewelPack(int packIndex) => false;
    }
}
