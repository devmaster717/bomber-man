using System;

namespace BombArena.Core
{
    /// <summary>
    /// A player's jewels and lives. This is the seam where a server-backed wallet replaces the local one
    /// (ADR 0002): gameplay and screens only talk to this interface.
    /// </summary>
    public interface IWallet
    {
        long Jewels { get; }
        int Lives { get; }

        /// <summary>Seconds until the next life regenerates; null when no life is regenerating (10 or more).</summary>
        long? SecondsToNextLife { get; }

        /// <summary>Applies regeneration up to now. Call on launch and before showing lives.</summary>
        void Refresh();

        void AddJewels(long amount);
        bool TrySpendJewels(long amount);
        void AddLives(int count);

        /// <summary>Starts a stage attempt; false (and nothing changes) when there are no lives.</summary>
        bool TryStartAttempt();

        /// <summary>Ends the running attempt. A failed attempt costs one life; a clear costs nothing.</summary>
        void EndAttempt(bool cleared);

        /// <summary>
        /// On launch: an attempt still marked as running was cut short (app killed), so it counts as failed.
        /// Returns true if a life was taken.
        /// </summary>
        bool RecoverInterruptedAttempt();
    }

    /// <summary>The wallet kept on the phone, using the phone's clock (ADR 0002).</summary>
    public sealed class LocalWallet : IWallet
    {
        /// <summary>Lives regenerate only while the player has fewer than this.</summary>
        public const int RegenCap = 10;

        /// <summary>One life every 5 minutes.</summary>
        public const long RegenSeconds = 5 * 60;

        private readonly PlayerProfile _profile;
        private readonly Func<long> _now;
        private readonly Action _save;

        /// <param name="now">Current time in Unix seconds.</param>
        /// <param name="save">Persists the profile after every change.</param>
        public LocalWallet(PlayerProfile profile, Func<long> now, Action save)
        {
            _profile = profile;
            _now = now;
            _save = save ?? (() => { });
            Refresh();
        }

        public long Jewels => _profile.Jewels;
        public int Lives => _profile.Lives;

        public long? SecondsToNextLife
        {
            get
            {
                if (_profile.RegenStartedAt is not long start) return null;
                return Math.Max(0, start + RegenSeconds - _now());
            }
        }

        public void Refresh()
        {
            long now = _now();
            if (_profile.Lives >= RegenCap)
            {
                _profile.RegenStartedAt = null;
                return;
            }

            long start = _profile.RegenStartedAt ?? now;
            if (start > now) start = now; // the clock went backwards; restart the period rather than freeze it
            long periods = (now - start) / RegenSeconds;
            if (periods > 0)
            {
                long add = Math.Min(periods, RegenCap - _profile.Lives);
                _profile.Lives += (int)add;
                start += periods * RegenSeconds;
            }
            _profile.RegenStartedAt = _profile.Lives >= RegenCap ? (long?)null : start;
            _save();
        }

        public void AddJewels(long amount)
        {
            _profile.Jewels += amount;
            _save();
        }

        public bool TrySpendJewels(long amount)
        {
            if (amount < 0 || _profile.Jewels < amount) return false;
            _profile.Jewels -= amount;
            _save();
            return true;
        }

        public void AddLives(int count)
        {
            Refresh();
            _profile.Lives += count;
            if (_profile.Lives >= RegenCap) _profile.RegenStartedAt = null;
            _save();
        }

        public bool TryStartAttempt()
        {
            Refresh();
            if (_profile.Lives <= 0) return false;
            _profile.AttemptInProgress = true;
            _save();
            return true;
        }

        public void EndAttempt(bool cleared)
        {
            if (!_profile.AttemptInProgress) return;
            _profile.AttemptInProgress = false;
            if (!cleared) LoseLife();
            _save();
        }

        public bool RecoverInterruptedAttempt()
        {
            if (!_profile.AttemptInProgress) return false;
            _profile.AttemptInProgress = false;
            LoseLife();
            _save();
            return true;
        }

        private void LoseLife()
        {
            Refresh();
            if (_profile.Lives <= 0) return;
            bool wasFull = _profile.Lives >= RegenCap;
            _profile.Lives--;
            if (_profile.Lives < RegenCap && (wasFull || _profile.RegenStartedAt == null))
                _profile.RegenStartedAt = _now();
        }
    }
}
