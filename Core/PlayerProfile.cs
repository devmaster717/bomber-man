using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BombArena.Core
{
    /// <summary>
    /// Everything saved about a player between sessions. Kept on the phone for now (ADR 0002); a server-backed
    /// wallet will replace the local one later.
    /// </summary>
    public sealed class PlayerProfile
    {
        public const long StartingJewels = 1000;
        public const int StartingLives = 10;

        public long Jewels { get; set; } = StartingJewels;
        public int Lives { get; set; } = StartingLives;

        /// <summary>When the current 5-minute regeneration period started (Unix seconds); null when lives are 10 or more.</summary>
        public long? RegenStartedAt { get; set; }

        /// <summary>True while a stage attempt is running, so an attempt cut short by closing the app counts as failed.</summary>
        public bool AttemptInProgress { get; set; }

        /// <summary>Best stars earned per stage number (1–100); missing means never cleared.</summary>
        public Dictionary<int, int> BestStars { get; } = new Dictionary<int, int>();

        /// <summary>Stages whose first 3-star clear bonus has already been paid.</summary>
        public HashSet<int> ThreeStarBonusPaid { get; } = new HashSet<int>();

        /// <summary>Power-ups held at the last clear, for the next stage attempt.</summary>
        public PowerUpLoadout CarriedPowerUps;

        /// <summary>Power-ups bought in the shop, applied at the start of the next attempt or round.</summary>
        public PowerUpLoadout Inventory;

        public string Nickname { get; set; } = "";
        public int Avatar { get; set; }
        public HashSet<int> OwnedAvatars { get; } = new HashSet<int>();

        public bool MusicOn { get; set; } = true;
        public bool SoundOn { get; set; } = true;
        public bool VibrationOn { get; set; } = true;
        public bool UseJoystick { get; set; }
        public int ButtonScalePercent { get; set; } = 100;
        public bool LeftHanded { get; set; }

        /// <summary>Saves as "key = value" lines, like the stage files.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            void Put(string k, object v) => sb.Append(k).Append(" = ").Append(System.Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\n');
            Put("jewels", Jewels);
            Put("lives", Lives);
            if (RegenStartedAt is long r) Put("regenStartedAt", r);
            Put("attemptInProgress", AttemptInProgress);
            foreach (var kv in BestStars) Put("stars." + kv.Key, kv.Value);
            if (ThreeStarBonusPaid.Count > 0) Put("threeStarBonusPaid", string.Join(",", ThreeStarBonusPaid));
            PutLoadout(Put, "carried", CarriedPowerUps);
            PutLoadout(Put, "inventory", Inventory);
            Put("nickname", Nickname.Replace("\n", " "));
            Put("avatar", Avatar);
            if (OwnedAvatars.Count > 0) Put("ownedAvatars", string.Join(",", OwnedAvatars));
            Put("music", MusicOn);
            Put("sound", SoundOn);
            Put("vibration", VibrationOn);
            Put("joystick", UseJoystick);
            Put("buttonScale", ButtonScalePercent);
            Put("leftHanded", LeftHanded);
            return sb.ToString();
        }

        private static void PutLoadout(System.Action<string, object> put, string prefix, PowerUpLoadout l)
        {
            put(prefix + ".fireUp", l.FireUp);
            put(prefix + ".bombUps", l.BombUps);
            put(prefix + ".remote", l.RemoteControl);
            put(prefix + ".speedTicks", l.SpeedUpTicksLeft);
        }

        /// <summary>Reads a saved profile; unknown or broken lines are skipped so an old save never blocks the game.</summary>
        public static PlayerProfile Parse(string text)
        {
            var p = new PlayerProfile();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                var key = raw.Substring(0, eq).Trim();
                var v = raw.Substring(eq + 1).Trim();
                bool isNumber = long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n);
                bool b = v == "True" || v == "true";
                if (!isNumber && NumericKeys.Contains(key.StartsWith("stars.") ? "stars" : key)) continue; // keep the default
                switch (key)
                {
                    case "jewels": p.Jewels = n; break;
                    case "lives": p.Lives = (int)n; break;
                    case "regenStartedAt": p.RegenStartedAt = n; break;
                    case "attemptInProgress": p.AttemptInProgress = b; break;
                    case "threeStarBonusPaid": foreach (var s in Ints(v)) p.ThreeStarBonusPaid.Add(s); break;
                    case "carried.fireUp": p.CarriedPowerUps.FireUp = b; break;
                    case "carried.bombUps": p.CarriedPowerUps.BombUps = (int)n; break;
                    case "carried.remote": p.CarriedPowerUps.RemoteControl = b; break;
                    case "carried.speedTicks": p.CarriedPowerUps.SpeedUpTicksLeft = (int)n; break;
                    case "inventory.fireUp": p.Inventory.FireUp = b; break;
                    case "inventory.bombUps": p.Inventory.BombUps = (int)n; break;
                    case "inventory.remote": p.Inventory.RemoteControl = b; break;
                    case "inventory.speedTicks": p.Inventory.SpeedUpTicksLeft = (int)n; break;
                    case "nickname": p.Nickname = v; break;
                    case "avatar": p.Avatar = (int)n; break;
                    case "ownedAvatars": foreach (var s in Ints(v)) p.OwnedAvatars.Add(s); break;
                    case "music": p.MusicOn = b; break;
                    case "sound": p.SoundOn = b; break;
                    case "vibration": p.VibrationOn = b; break;
                    case "joystick": p.UseJoystick = b; break;
                    case "buttonScale": p.ButtonScalePercent = (int)n; break;
                    case "leftHanded": p.LeftHanded = b; break;
                    default:
                        if (key.StartsWith("stars.") && int.TryParse(key.Substring(6), out int stage))
                            p.BestStars[stage] = (int)n;
                        break;
                }
            }
            return p;
        }

        private static readonly HashSet<string> NumericKeys = new HashSet<string>
        {
            "jewels", "lives", "regenStartedAt", "carried.bombUps", "carried.speedTicks", "inventory.bombUps",
            "inventory.speedTicks", "avatar", "buttonScale", "stars",
        };

        private static IEnumerable<int> Ints(string csv)
        {
            foreach (var s in csv.Split(','))
                if (int.TryParse(s.Trim(), out int i)) yield return i;
        }
    }
}
