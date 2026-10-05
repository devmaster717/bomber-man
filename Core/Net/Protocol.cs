using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BombArena.Core.Net
{
    public enum MessageType : byte
    {
        Hello = 1,
        Lobby = 2,
        Rejected = 3,
        Start = 4,
        Input = 5,
        Snapshot = 6,
        Forfeit = 7,
        Away = 8,
        Back = 9,
        Paused = 10,
        Resumed = 11,
        Result = 12,
    }

    /// <summary>A player as the room sees them.</summary>
    public sealed class PlayerInfo
    {
        public string Nickname = "";
        public int Avatar;
        public long Jewels;
        public PowerUpLoadout Inventory;

        public PlayerInfo Clone() => (PlayerInfo)MemberwiseClone();
    }

    /// <summary>What the host chooses for a round (spec section 8).</summary>
    public sealed class RoundSettings
    {
        public int Width = Arena.DefaultWidth;
        public int Height = Arena.DefaultHeight;

        /// <summary>0 means unlimited (the default).</summary>
        public int TimeLimitSeconds;

        public long EntryFee = 100;
        public bool EnemiesOn;

        public static readonly int[] TimeLimitChoices = { 0, 120, 180, 300 };
        public static readonly long[] EntryFeeChoices = { 100, 250, 500, 1000 };

        public RoundSettings Clone() => (RoundSettings)MemberwiseClone();
    }

    /// <summary>
    /// Encodes and decodes messages: one type byte, the protocol version, then the body. Phones running
    /// different builds refuse each other instead of misreading.
    /// </summary>
    public static class Protocol
    {
        public const byte Version = 1;

        public static MessageType TypeOf(byte[] message) => (MessageType)message[0];

        public static byte[] Write(MessageType type, Action<BinaryWriter> body = null)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write((byte)type);
                w.Write(Version);
                body?.Invoke(w);
            }
            return ms.ToArray();
        }

        /// <summary>Opens a message for reading its body; throws if it came from a different protocol version.</summary>
        public static BinaryReader Read(byte[] message)
        {
            if (message.Length < 2) throw new InvalidDataException("Message too short");
            if (message[1] != Version) throw new InvalidDataException($"Protocol version {message[1]}, expected {Version}");
            var r = new BinaryReader(new MemoryStream(message, 2, message.Length - 2), Encoding.UTF8);
            return r;
        }

        // ---- shared pieces ----

        public static void WritePlayer(BinaryWriter w, PlayerInfo p)
        {
            w.Write(p.Nickname ?? "");
            w.Write((byte)p.Avatar);
            w.Write(p.Jewels);
            WriteLoadout(w, p.Inventory);
        }

        public static PlayerInfo ReadPlayer(BinaryReader r) => new PlayerInfo
        {
            Nickname = PlayerProfile.CleanNickname(r.ReadString()),
            Avatar = Math.Min((int)r.ReadByte(), PlayerProfile.AvatarCount - 1),
            Jewels = r.ReadInt64(),
            Inventory = ReadLoadout(r),
        };

        public static void WriteLoadout(BinaryWriter w, PowerUpLoadout l)
        {
            w.Write(l.FireUp);
            w.Write((byte)l.BombUps);
            w.Write(l.RemoteControl);
            w.Write(l.SpeedUpTicksLeft);
        }

        public static PowerUpLoadout ReadLoadout(BinaryReader r) => new PowerUpLoadout
        {
            FireUp = r.ReadBoolean(),
            BombUps = Math.Min((int)r.ReadByte(), Bomber.BombCap - 1),
            RemoteControl = r.ReadBoolean(),
            SpeedUpTicksLeft = Math.Min(r.ReadInt32(), Bomber.SpeedUpTicks),
        };

        public static void WriteSettings(BinaryWriter w, RoundSettings s)
        {
            w.Write((byte)s.Width);
            w.Write((byte)s.Height);
            w.Write((short)s.TimeLimitSeconds);
            w.Write(s.EntryFee);
            w.Write(s.EnemiesOn);
        }

        public static RoundSettings ReadSettings(BinaryReader r) => new RoundSettings
        {
            Width = r.ReadByte(),
            Height = r.ReadByte(),
            TimeLimitSeconds = r.ReadInt16(),
            EntryFee = r.ReadInt64(),
            EnemiesOn = r.ReadBoolean(),
        };

        // ---- lobby messages ----

        public static byte[] Hello(PlayerInfo me) => Write(MessageType.Hello, w => WritePlayer(w, me));

        public static byte[] Lobby(IReadOnlyList<PlayerInfo> players, RoundSettings settings, int yourIndex) =>
            Write(MessageType.Lobby, w =>
            {
                w.Write((byte)yourIndex);
                WriteSettings(w, settings);
                w.Write((byte)players.Count);
                foreach (var p in players) WritePlayer(w, p);
            });

        public static (List<PlayerInfo> players, RoundSettings settings, int yourIndex) ReadLobby(byte[] m)
        {
            using var r = Read(m);
            int you = r.ReadByte();
            var settings = ReadSettings(r);
            int n = r.ReadByte();
            var players = new List<PlayerInfo>();
            for (int i = 0; i < n; i++) players.Add(ReadPlayer(r));
            return (players, settings, you);
        }

        public static byte[] Rejected(string reason) => Write(MessageType.Rejected, w => w.Write(reason));

        public static string ReadRejected(byte[] m)
        {
            using var r = Read(m);
            return r.ReadString();
        }
    }
}
