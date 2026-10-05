using System;
using System.Collections.Generic;
using System.IO;

namespace BombArena.Core.Net
{
    /// <summary>
    /// The host's side of a room before the round starts: accepts up to two guests, learns who they are from
    /// their Hello, and keeps every phone's player list and settings in step.
    /// </summary>
    public sealed class LobbyHost
    {
        public const int MaxGuests = 2;
        public const string RoomFull = "The room is full.";
        public const string WrongVersion = "That phone runs a different version of Bomb Arena.";

        private readonly IHostTransport _transport;
        private readonly List<PlayerInfo> _players = new List<PlayerInfo>();
        private readonly List<int> _peers = new List<int>(); // peer per guest, in player order (player i+1)
        private readonly HashSet<int> _waitingForHello = new HashSet<int>();

        public RoundSettings Settings { get; private set; }

        /// <summary>Player 0 is the host; guests follow in the order they joined.</summary>
        public IReadOnlyList<PlayerInfo> Players => _players;

        /// <summary>The transport peer for each guest (index 0 is player 1).</summary>
        public IReadOnlyList<int> GuestPeers => _peers;

        public event Action Changed;

        public LobbyHost(IHostTransport transport, PlayerInfo host, RoundSettings settings)
        {
            _transport = transport;
            _players.Add(host);
            Settings = settings.Clone();
            _transport.PeerConnected += OnConnected;
            _transport.Received += OnReceived;
            _transport.PeerDisconnected += OnDisconnected;
        }

        /// <summary>Stops listening, e.g. when the round starts and a round host takes over the transport.</summary>
        public void Detach()
        {
            _transport.PeerConnected -= OnConnected;
            _transport.Received -= OnReceived;
            _transport.PeerDisconnected -= OnDisconnected;
        }

        public void ChangeSettings(RoundSettings settings)
        {
            Settings = settings.Clone();
            Broadcast();
        }

        /// <summary>Hook for the entry-fee rule (#16): return a reason to refuse a player, or null to let them in.</summary>
        public Func<PlayerInfo, RoundSettings, string> Admission { get; set; } = (p, s) => null;

        private void OnConnected(int peer)
        {
            if (_peers.Count + _waitingForHello.Count >= MaxGuests)
            {
                _transport.Send(peer, Protocol.Rejected(RoomFull));
                _transport.Disconnect(peer);
                return;
            }
            _waitingForHello.Add(peer);
        }

        private void OnReceived(int peer, byte[] message)
        {
            if (Protocol.TypeOf(message) != MessageType.Hello || !_waitingForHello.Contains(peer)) return;
            _waitingForHello.Remove(peer);

            PlayerInfo player;
            try
            {
                using var r = Protocol.Read(message);
                player = Protocol.ReadPlayer(r);
            }
            catch (InvalidDataException)
            {
                _transport.Send(peer, Protocol.Rejected(WrongVersion));
                _transport.Disconnect(peer);
                return;
            }

            var refusal = Admission(player, Settings);
            if (refusal != null)
            {
                _transport.Send(peer, Protocol.Rejected(refusal));
                _transport.Disconnect(peer);
                return;
            }

            _players.Add(player);
            _peers.Add(peer);
            Broadcast();
        }

        private void OnDisconnected(int peer)
        {
            _waitingForHello.Remove(peer);
            int i = _peers.IndexOf(peer);
            if (i < 0) return;
            _peers.RemoveAt(i);
            _players.RemoveAt(i + 1);
            Broadcast();
        }

        private void Broadcast()
        {
            for (int i = 0; i < _peers.Count; i++)
                _transport.Send(_peers[i], Protocol.Lobby(_players, Settings, i + 1));
            Changed?.Invoke();
        }
    }

    /// <summary>A guest's view of the room: who is in it and the host's settings.</summary>
    public sealed class LobbyGuest
    {
        private readonly IGuestTransport _transport;

        public List<PlayerInfo> Players { get; private set; } = new List<PlayerInfo>();
        public RoundSettings Settings { get; private set; } = new RoundSettings();

        /// <summary>This phone's player index once the host has accepted it; -1 before that.</summary>
        public int YourIndex { get; private set; } = -1;

        /// <summary>Why the host refused us, if it did.</summary>
        public string Rejection { get; private set; }

        public bool Connected { get; private set; } = true;

        public event Action Changed;

        /// <summary>Raised with the raw message for anything the lobby does not handle (e.g. the round starting).</summary>
        public event Action<byte[]> Other;

        public LobbyGuest(IGuestTransport transport, PlayerInfo me)
        {
            _transport = transport;
            _transport.Received += OnReceived;
            _transport.Disconnected += OnDisconnected;
            _transport.Send(Protocol.Hello(me));
        }

        public void Detach()
        {
            _transport.Received -= OnReceived;
            _transport.Disconnected -= OnDisconnected;
        }

        public void Leave() => _transport.Disconnect();

        private void OnReceived(byte[] message)
        {
            try
            {
                switch (Protocol.TypeOf(message))
                {
                    case MessageType.Lobby:
                        var (players, settings, you) = Protocol.ReadLobby(message);
                        Players = players;
                        Settings = settings;
                        YourIndex = you;
                        Changed?.Invoke();
                        break;
                    case MessageType.Rejected:
                        Rejection = Protocol.ReadRejected(message);
                        Changed?.Invoke();
                        break;
                    default:
                        Other?.Invoke(message);
                        break;
                }
            }
            catch (InvalidDataException)
            {
                Rejection = LobbyHost.WrongVersion;
                Changed?.Invoke();
            }
        }

        private void OnDisconnected()
        {
            Connected = false;
            Changed?.Invoke();
        }
    }
}
