using System;
using System.Collections.Generic;
using System.IO;

namespace BombArena.Core.Net
{
    /// <summary>Round messages: Start (seed and players), guest Input, host Snapshot, and Result.</summary>
    public static class RoundMessages
    {
        public static byte[] Start(ulong seed, RoundSettings settings, IReadOnlyList<PlayerInfo> players, int yourIndex) =>
            Protocol.Write(MessageType.Start, w =>
            {
                w.Write(seed);
                Protocol.WriteSettings(w, settings);
                w.Write((byte)yourIndex);
                w.Write((byte)players.Count);
                foreach (var p in players) Protocol.WritePlayer(w, p);
            });

        public static (ulong seed, RoundSettings settings, int yourIndex, List<PlayerInfo> players) ReadStart(byte[] m)
        {
            using var r = Protocol.Read(m);
            ulong seed = r.ReadUInt64();
            var settings = Protocol.ReadSettings(r);
            int you = r.ReadByte();
            int n = r.ReadByte();
            var players = new List<PlayerInfo>();
            for (int i = 0; i < n; i++) players.Add(Protocol.ReadPlayer(r));
            return (seed, settings, you, players);
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
    }

    /// <summary>
    /// Runs a round on the host's phone (spec section 8): the only copy of the rules that runs. Each tick it steps
    /// the game with every player's latest input and sends the whole state to the guests.
    /// </summary>
    public sealed class RoundHost
    {
        private readonly IHostTransport _transport;
        private readonly List<int> _peers;
        private readonly Direction[] _move;
        private readonly bool[] _bomb, _detonate;

        public Game Game { get; }
        public IReadOnlyList<PlayerInfo> Players { get; }
        public RoundSettings Settings { get; }

        /// <summary>Raised once when the round ends, with the winner (null for a draw).</summary>
        public event Action<int?> Finished;

        private bool _finished;

        /// <param name="players">Player 0 is the host; player i + 1 is reached through <paramref name="guestPeers"/>[i].</param>
        public RoundHost(IHostTransport transport, IReadOnlyList<PlayerInfo> players, IReadOnlyList<int> guestPeers,
            RoundSettings settings, ulong seed)
        {
            _transport = transport;
            _peers = new List<int>(guestPeers);
            Players = players;
            Settings = settings.Clone();
            Game = Game.ForRound(settings.Width, settings.Height, players.Count, settings.TimeLimitSeconds, seed);
            _move = new Direction[players.Count];
            _bomb = new bool[players.Count];
            _detonate = new bool[players.Count];

            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnDisconnected;
            for (int i = 0; i < _peers.Count; i++)
                _transport.Send(_peers[i], RoundMessages.Start(seed, Settings, players, i + 1));
        }

        public void Detach()
        {
            _transport.Received -= OnReceived;
            _transport.PeerDisconnected -= OnDisconnected;
        }

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
            if (_finished) return;
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

        private void CheckFinished()
        {
            if (_finished || Game.Outcome != Outcome.RoundOver) return;
            _finished = true;
            Broadcast(RoundMessages.Result(Game.Winner));
            Finished?.Invoke(Game.Winner);
        }

        private void Broadcast(byte[] m)
        {
            foreach (var p in _peers)
                if (p >= 0) _transport.Send(p, m);
        }

        private void OnReceived(int peer, byte[] m)
        {
            int player = _peers.IndexOf(peer) + 1;
            if (player <= 0) return;
            try
            {
                if (Protocol.TypeOf(m) == MessageType.Input)
                {
                    var (move, bomb, detonate) = RoundMessages.ReadInput(m);
                    Take(player, move, bomb, detonate);
                }
            }
            catch (InvalidDataException)
            {
            }
        }

        /// <summary>A guest's link dropped: for now their bomber is out (pausing and reconnecting come in #18).</summary>
        private void OnDisconnected(int peer)
        {
            int i = _peers.IndexOf(peer);
            if (i < 0) return;
            _peers[i] = -1;
            Game.Forfeit(i + 1);
            CheckFinished();
        }
    }

    /// <summary>
    /// A guest's side of a round: mirrors the host's state for drawing and sends this player's controls.
    /// </summary>
    public sealed class RoundGuest
    {
        private readonly IGuestTransport _transport;
        private Direction _lastSent = Direction.None;

        public Game Game { get; }
        public int YourIndex { get; }
        public List<PlayerInfo> Players { get; }
        public RoundSettings Settings { get; }

        /// <summary>The winner once the host reports the result (null for a draw).</summary>
        public int? Winner { get; private set; }
        public bool Finished { get; private set; }

        /// <summary>True once the link to the host is gone; the round cannot continue without the host.</summary>
        public bool HostLost { get; private set; }

        /// <summary>Raised after each state update from the host.</summary>
        public event Action Updated;

        /// <summary>Builds the guest's copy of the round from the host's Start message.</summary>
        public RoundGuest(IGuestTransport transport, byte[] startMessage)
        {
            _transport = transport;
            var (seed, settings, you, players) = RoundMessages.ReadStart(startMessage);
            Settings = settings;
            YourIndex = you;
            Players = players;
            Game = Game.ForRound(settings.Width, settings.Height, players.Count, settings.TimeLimitSeconds, seed);
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
            if (Finished || HostLost) return;
            if (move == _lastSent && !bomb && !detonate) return;
            _lastSent = move;
            _transport.Send(RoundMessages.Input(move, bomb, detonate));
        }

        private void OnReceived(byte[] m)
        {
            try
            {
                switch (Protocol.TypeOf(m))
                {
                    case MessageType.Snapshot:
                        RoundMessages.ApplySnapshot(Game, m);
                        Updated?.Invoke();
                        break;
                    case MessageType.Result:
                        Winner = RoundMessages.ReadResult(m);
                        Finished = true;
                        Updated?.Invoke();
                        break;
                }
            }
            catch (InvalidDataException)
            {
            }
        }

        private void OnDisconnected()
        {
            HostLost = !Finished;
            Updated?.Invoke();
        }
    }
}
