using System;
using System.Collections.Generic;
using System.IO;

namespace BombArena.Core.Net
{
    /// <summary>
    /// Round messages: Start (seed, players and a private rejoin token), guest Input/Forfeit/Away/Back/Rejoin, and
    /// host Snapshot/Paused/Resumed/Result.
    /// </summary>
    public static class RoundMessages
    {
        public static byte[] Start(ulong seed, RoundSettings settings, IReadOnlyList<PlayerInfo> players, int yourIndex, long rejoinToken) =>
            Protocol.Write(MessageType.Start, w =>
            {
                w.Write(seed);
                Protocol.WriteSettings(w, settings);
                w.Write((byte)yourIndex);
                w.Write(rejoinToken);
                w.Write((byte)players.Count);
                foreach (var p in players) Protocol.WritePlayer(w, p);
            });

        public static (ulong seed, RoundSettings settings, int yourIndex, long token, List<PlayerInfo> players) ReadStart(byte[] m)
        {
            using var r = Protocol.Read(m);
            ulong seed = r.ReadUInt64();
            var settings = Protocol.ReadSettings(r);
            int you = r.ReadByte();
            long token = r.ReadInt64();
            int n = r.ReadByte();
            var players = new List<PlayerInfo>();
            for (int i = 0; i < n; i++) players.Add(Protocol.ReadPlayer(r));
            return (seed, settings, you, token, players);
        }

        public static byte[] Input(Direction move, bool bomb, bool detonate) =>
            Protocol.Write(MessageType.Input, w =>
            {
                w.Write((byte)move);
                w.Write(bomb);
                w.Write(detonate);
            });

        public static (Direction move, bool bomb, bool detonate) ReadInput(byte[] m)
        {
            using var r = Protocol.Read(m);
            var d = (Direction)r.ReadByte();
            if (d < Direction.None || d > Direction.Right) d = Direction.None;
            return (d, r.ReadBoolean(), r.ReadBoolean());
        }

        public static byte[] Snapshot(Game game) => Protocol.Write(MessageType.Snapshot, game.WriteState);

        public static void ApplySnapshot(Game game, byte[] m)
        {
            using var r = Protocol.Read(m);
            game.ReadState(r);
        }

        public static byte[] Result(int? winner) => Protocol.Write(MessageType.Result, w => w.Write((sbyte)(winner ?? -1)));

        public static int? ReadResult(byte[] m)
        {
            using var r = Protocol.Read(m);
            int w = r.ReadSByte();
            return w < 0 ? (int?)null : w;
        }

        public static byte[] Forfeit() => Protocol.Write(MessageType.Forfeit);
        public static byte[] Away() => Protocol.Write(MessageType.Away);
        public static byte[] Back() => Protocol.Write(MessageType.Back);
        public static byte[] Rejoin(long token) => Protocol.Write(MessageType.Rejoin, w => w.Write(token));

        public static long ReadRejoin(byte[] m)
        {
            using var r = Protocol.Read(m);
            return r.ReadInt64();
        }

        /// <summary>The round is paused while waiting for a player (index), or for the host itself (index 0).</summary>
        public static byte[] Paused(int waitingFor) => Protocol.Write(MessageType.Paused, w => w.Write((byte)waitingFor));

        public static int ReadPaused(byte[] m)
        {
            using var r = Protocol.Read(m);
            return r.ReadByte();
        }

        public static byte[] Resumed() => Protocol.Write(MessageType.Resumed);
    }

    /// <summary>
    /// Runs a round on the host's phone (spec section 8): the only copy of the rules that runs. Each tick it steps
    /// the game with every player's latest input and sends the whole state to the guests. A guest who leaves the
    /// app or loses the link pauses the round for everyone until they return; after 60 s the host may count them
    /// as a forfeit. Nobody can pause on purpose.
    /// </summary>
    public sealed class RoundHost
    {
        /// <summary>How long the host must wait for a missing guest before counting them as a forfeit.</summary>
        public const double ForfeitAfterSeconds = 60;

        private readonly IHostTransport _transport;
        private readonly int[] _peers;          // peer per player; -1 when not connected (index 0 is the host)
        private readonly long[] _tokens;        // private rejoin token per player
        private readonly bool[] _away;          // player is away (left the app or link dropped)
        private readonly double[] _awaySince;   // clock time each absence started
        private readonly Direction[] _move;
        private readonly bool[] _bomb, _detonate;
        private bool _finished, _hostAway;
        private int? _announcedWait;

        public Game Game { get; }
        public IReadOnlyList<PlayerInfo> Players { get; }
        public RoundSettings Settings { get; }

        /// <summary>The host app's clock in seconds (e.g. unscaled time); used for the 60-second rule.</summary>
        public Func<double> Clock { get; set; } = () => 0;

        /// <summary>Raised once when the round ends, with the winner (null for a draw).</summary>
        public event Action<int?> Finished;

        /// <param name="players">Player 0 is the host; player i + 1 is reached through <paramref name="guestPeers"/>[i].</param>
        public RoundHost(IHostTransport transport, IReadOnlyList<PlayerInfo> players, IReadOnlyList<int> guestPeers,
            RoundSettings settings, ulong seed)
        {
            _transport = transport;
            Players = players;
            Settings = settings.Clone();
            int n = players.Count;
            _peers = new int[n];
            _tokens = new long[n];
            _away = new bool[n];
            _awaySince = new double[n];
            _move = new Direction[n];
            _bomb = new bool[n];
            _detonate = new bool[n];

            Game = Game.ForRound(settings.Width, settings.Height, n, settings.TimeLimitSeconds, seed, settings.EnemiesOn);
            // Power-ups each player bought in the shop start the round with them.
            for (int i = 0; i < n; i++) Game.ApplyLoadout(i, players[i].Inventory);

            var tokenRng = new Rng(seed ^ (ulong)DateTime.UtcNow.Ticks);
            _peers[0] = -1;
            for (int i = 1; i < n; i++)
            {
                _peers[i] = guestPeers[i - 1];
                _tokens[i] = (long)(tokenRng.NextULong() & long.MaxValue);
            }

            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnDisconnected;
            for (int i = 1; i < n; i++)
                _transport.Send(_peers[i], RoundMessages.Start(seed, Settings, players, i, _tokens[i]));
        }

        public void Detach()
        {
            _transport.Received -= OnReceived;
            _transport.PeerDisconnected -= OnDisconnected;
        }

        /// <summary>True while the round waits for a player who left the app or lost the link (or for the host).</summary>
        public bool Paused => _hostAway || WaitingFor != null;

        /// <summary>The first player the round is waiting for, if any (not counting the host).</summary>
        public int? WaitingFor
        {
            get
            {
                for (int i = 1; i < _away.Length; i++)
                    if (_away[i] && Game.Bombers[i].Alive) return i;
                return null;
            }
        }

        /// <summary>Seconds the round has waited for the missing player.</summary>
        public double WaitedSeconds => WaitingFor is int i ? Clock() - _awaySince[i] : 0;

        /// <summary>The host may count the missing player as a forfeit once they have been gone 60 seconds.</summary>
        public bool CanForfeitMissing => WaitingFor != null && WaitedSeconds >= ForfeitAfterSeconds;

        /// <summary>The host player's own controls (player 0). Presses are kept until the next tick uses them.</summary>
        public void SetHostInput(Direction move, bool bomb, bool detonate) => Take(0, move, bomb, detonate);

        private void Take(int player, Direction move, bool bomb, bool detonate)
        {
            _move[player] = move;
            _bomb[player] |= bomb;
            _detonate[player] |= detonate;
        }

        /// <summary>Advances the round one tick and sends the new state to every guest. Call 20 times a second.</summary>
        public void Tick()
        {
            if (_finished || Paused) return;
            var inputs = new BomberInput[Players.Count];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new BomberInput(_move[i], _bomb[i], _detonate[i]);
                _bomb[i] = _detonate[i] = false;
            }
            Game.Step(inputs);
            Broadcast(RoundMessages.Snapshot(Game));
            CheckFinished();
        }

        /// <summary>The host leaves the round on purpose: the host's bomber is out, but this phone keeps running it.</summary>
        public void ForfeitHost() => ForfeitPlayer(0);

        /// <summary>After 60 s, counts the missing guest as a forfeit and lets the round continue.</summary>
        public bool ForfeitMissing()
        {
            if (!CanForfeitMissing) return false;
            int i = WaitingFor.Value;
            _away[i] = false;
            ForfeitPlayer(i);
            AnnounceWait();
            return true;
        }

        /// <summary>The host's own app went to the background (or came back): everyone waits for the host.</summary>
        public void SetHostAway(bool away)
        {
            if (_hostAway == away) return;
            _hostAway = away;
            AnnounceWait();
        }

        private void ForfeitPlayer(int i)
        {
            Game.Forfeit(i);
            Broadcast(RoundMessages.Snapshot(Game));
            CheckFinished();
        }

        private void CheckFinished()
        {
            if (_finished || Game.Outcome != Outcome.RoundOver) return;
            _finished = true;
            Broadcast(RoundMessages.Result(Game.Winner));
            Finished?.Invoke(Game.Winner);
        }

        private void Broadcast(byte[] m)
        {
            for (int i = 1; i < _peers.Length; i++)
                if (_peers[i] >= 0) _transport.Send(_peers[i], m);
        }

        /// <summary>Tells everyone who the round is waiting for, or that it has resumed, when that changes.</summary>
        private void AnnounceWait()
        {
            int? wait = _hostAway ? 0 : WaitingFor;
            if (wait == _announcedWait) return;
            _announcedWait = wait;
            Broadcast(wait is int w ? RoundMessages.Paused(w) : RoundMessages.Resumed());
        }

        private void SetAway(int player, bool away)
        {
            if (_away[player] == away) return;
            _away[player] = away;
            if (away) _awaySince[player] = Clock();
            AnnounceWait();
        }

        private void OnReceived(int peer, byte[] m)
        {
            try
            {
                var type = Protocol.TypeOf(m);
                if (type == MessageType.Rejoin)
                {
                    Rejoin(peer, RoundMessages.ReadRejoin(m));
                    return;
                }

                int player = Array.IndexOf(_peers, peer);
                if (player <= 0) return;
                switch (type)
                {
                    case MessageType.Input:
                        var (move, bomb, detonate) = RoundMessages.ReadInput(m);
                        Take(player, move, bomb, detonate);
                        break;
                    case MessageType.Forfeit:
                        ForfeitPlayer(player);
                        AnnounceWait();
                        break;
                    case MessageType.Away:
                        SetAway(player, true);
                        break;
                    case MessageType.Back:
                        SetAway(player, false);
                        break;
                }
            }
            catch (InvalidDataException)
            {
            }
        }

        /// <summary>A guest's link dropped: the round waits for them to reconnect (or for the host to count them out).</summary>
        private void OnDisconnected(int peer)
        {
            int i = Array.IndexOf(_peers, peer);
            if (i <= 0) return;
            _peers[i] = -1;
            if (!_finished) SetAway(i, true);
        }

        /// <summary>A guest reconnects with the private token from its Start message and takes back its bomber.</summary>
        private void Rejoin(int peer, long token)
        {
            int i = Array.IndexOf(_tokens, token);
            if (i <= 0 || _peers[i] >= 0 || _finished)
            {
                _transport.Send(peer, Protocol.Rejected(RejoinRefused));
                _transport.Disconnect(peer);
                return;
            }
            _peers[i] = peer;
            _transport.Send(peer, RoundMessages.Snapshot(Game));
            SetAway(i, false);
            if (_announcedWait is int w) _transport.Send(peer, RoundMessages.Paused(w));
        }

        public const string RejoinRefused = "That round has ended or your place was taken.";
    }

    /// <summary>
    /// A guest's side of a round: mirrors the host's state for drawing and sends this player's controls. If the link
    /// drops it can rejoin with its private token.
    /// </summary>
    public sealed class RoundGuest
    {
        private IGuestTransport _transport;
        private Direction _lastSent = Direction.None;
        private readonly long _token;

        public Game Game { get; }
        public int YourIndex { get; }
        public List<PlayerInfo> Players { get; }
        public RoundSettings Settings { get; }

        /// <summary>The winner once the host reports the result (null for a draw).</summary>
        public int? Winner { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>True while the link to the host is down; <see cref="Rejoin"/> can restore it.</summary>
        public bool LinkLost { get; private set; }

        /// <summary>True when the round cannot continue: the link is down and was not restored.</summary>
        public bool HostLost => LinkLost && !Finished;

        /// <summary>Who the round is waiting for while paused (0 means the host); null while running.</summary>
        public int? PausedFor { get; private set; }

        /// <summary>Raised after each update from the host.</summary>
        public event Action Updated;

        /// <summary>Builds the guest's copy of the round from the host's Start message.</summary>
        public RoundGuest(IGuestTransport transport, byte[] startMessage)
        {
            var (seed, settings, you, token, players) = RoundMessages.ReadStart(startMessage);
            Settings = settings;
            YourIndex = you;
            Players = players;
            _token = token;
            Game = Game.ForRound(settings.Width, settings.Height, players.Count, settings.TimeLimitSeconds, seed, settings.EnemiesOn);
            for (int i = 0; i < players.Count; i++) Game.ApplyLoadout(i, players[i].Inventory);
            Attach(transport);
        }

        private void Attach(IGuestTransport transport)
        {
            _transport = transport;
            _transport.Received += OnReceived;
            _transport.Disconnected += OnDisconnected;
        }

        public void Detach()
        {
            _transport.Received -= OnReceived;
            _transport.Disconnected -= OnDisconnected;
        }

        /// <summary>Sends this player's controls; only changes and presses go over the air.</summary>
        public void SendInput(Direction move, bool bomb, bool detonate)
        {
            if (Finished || LinkLost) return;
            if (move == _lastSent && !bomb && !detonate) return;
            _lastSent = move;
            _transport.Send(RoundMessages.Input(move, bomb, detonate));
        }

        /// <summary>Leaves the round on purpose: this player's bomber is out and the entry fee stays in the pot.</summary>
        public void Forfeit()
        {
            if (!LinkLost) _transport.Send(RoundMessages.Forfeit());
        }

        /// <summary>The app went to the background (true) or came back (false): the host pauses the round meanwhile.</summary>
        public void SetAway(bool away)
        {
            if (!LinkLost && !Finished) _transport.Send(away ? RoundMessages.Away() : RoundMessages.Back());
        }

        /// <summary>After the link dropped: reconnects to the host on a new link and takes back this player's bomber.</summary>
        public void Rejoin(IGuestTransport newLink)
        {
            Detach();
            Attach(newLink);
            LinkLost = false;
            _lastSent = Direction.None;
            _transport.Send(RoundMessages.Rejoin(_token));
        }

        private void OnReceived(byte[] m)
        {
            try
            {
                switch (Protocol.TypeOf(m))
                {
                    case MessageType.Snapshot:
                        RoundMessages.ApplySnapshot(Game, m);
                        break;
                    case MessageType.Result:
                        Winner = RoundMessages.ReadResult(m);
                        Finished = true;
                        PausedFor = null;
                        break;
                    case MessageType.Paused:
                        PausedFor = RoundMessages.ReadPaused(m);
                        break;
                    case MessageType.Resumed:
                        PausedFor = null;
                        break;
                    case MessageType.Rejected:
                        LinkLost = true;
                        break;
                    default:
                        return;
                }
                Updated?.Invoke();
            }
            catch (InvalidDataException)
            {
            }
        }

        private void OnDisconnected()
        {
            LinkLost = true;
            Updated?.Invoke();
        }
    }
}
