using System;
using System.Collections.Generic;

namespace BombArena.Core.Net
{
    /// <summary>
    /// What the host needs from a connection layer: whole messages to and from numbered peers. The Bluetooth
    /// RFCOMM plugin implements it on Android; <see cref="MemoryLink"/> implements it in tests.
    /// </summary>
    public interface IHostTransport
    {
        event Action<int> PeerConnected;
        event Action<int, byte[]> Received;
        event Action<int> PeerDisconnected;
        void Send(int peer, byte[] message);
        void Disconnect(int peer);
    }

    /// <summary>What a guest needs: whole messages to and from the host.</summary>
    public interface IGuestTransport
    {
        event Action<byte[]> Received;
        event Action Disconnected;
        void Send(byte[] message);
        void Disconnect();
    }

    /// <summary>
    /// An in-process host with any number of guests, delivering messages when <see cref="Pump"/> is called, in
    /// order. Used by tests to run real host and guest logic without phones.
    /// </summary>
    public sealed class MemoryLink : IHostTransport
    {
        public event Action<int> PeerConnected;
        public event Action<int, byte[]> Received;
        public event Action<int> PeerDisconnected;

        private readonly Queue<Action> _pending = new Queue<Action>();
        private readonly Dictionary<int, Guest> _guests = new Dictionary<int, Guest>();
        private int _nextPeer = 1;

        /// <summary>Connects a new guest; the host hears about it on the next pump.</summary>
        public Guest Connect()
        {
            var g = new Guest(this, _nextPeer++);
            _guests[g.Peer] = g;
            _pending.Enqueue(() => PeerConnected?.Invoke(g.Peer));
            return g;
        }

        public void Send(int peer, byte[] message)
        {
            if (_guests.TryGetValue(peer, out var g) && g.Open)
                _pending.Enqueue(() => { if (g.Open) g.Deliver(message); });
        }

        /// <summary>A graceful close: messages already sent are delivered first (the plugin flushes before closing too).</summary>
        public void Disconnect(int peer)
        {
            if (_guests.TryGetValue(peer, out var g)) _pending.Enqueue(() => g.Drop(notifyHost: true));
        }

        /// <summary>Delivers every queued message and event (including ones queued while delivering).</summary>
        public void Pump()
        {
            int guard = 0;
            while (_pending.Count > 0 && guard++ < 100_000) _pending.Dequeue()();
        }

        public sealed class Guest : IGuestTransport
        {
            private readonly MemoryLink _link;
            public int Peer { get; }
            public bool Open { get; private set; } = true;

            public event Action<byte[]> Received;
            public event Action Disconnected;

            internal Guest(MemoryLink link, int peer)
            {
                _link = link;
                Peer = peer;
            }

            internal void Deliver(byte[] m) => Received?.Invoke(m);

            public void Send(byte[] message)
            {
                if (!Open) return;
                _link._pending.Enqueue(() => { if (Open) _link.Received?.Invoke(Peer, message); });
            }

            public void Disconnect() => Drop(notifyHost: true);

            /// <summary>Simulates the radio link dropping: both sides are told.</summary>
            public void Drop(bool notifyHost)
            {
                if (!Open) return;
                Open = false;
                _link._pending.Enqueue(() => Disconnected?.Invoke());
                if (notifyHost) _link._pending.Enqueue(() => _link.PeerDisconnected?.Invoke(Peer));
            }
        }
    }
}
