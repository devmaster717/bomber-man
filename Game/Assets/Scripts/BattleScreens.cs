using System;
using BombArena.Core;
using BombArena.Core.Net;
using UnityEngine;

/// <summary>
/// The Bluetooth battle screens (spec sections 8 and 10): choose Host or Join, the host's room, the list of
/// nearby phones, and the guest's view of the room. Owned by <see cref="AppController"/>.
/// </summary>
public sealed class BattleScreens
{
    private enum Step
    {
        Menu,
        Hosting,
        Scanning,
        Joining,
        InRoom,
        Playing,
    }

    private readonly PlayerProfile _profile;
    private readonly IWallet _wallet;
    private BluetoothTransport _bt;
    private LobbyHost _host;
    private LobbyGuest _guest;
    private IGuestTransport _guestLink;
    private RoundGuest _roundGuest;
    private string _hostAddress;
    private bool _rejoining;
    private RoundView _round;
    private long _fee;
    private long _payout;
    private Step _step = Step.Menu;
    private string _status;
    private bool _permissionsOk;

    /// <summary>Raised when the player leaves the Bluetooth screens.</summary>
    public event Action Exit;

    public BattleScreens(PlayerProfile profile, IWallet wallet)
    {
        _profile = profile;
        _wallet = wallet;
    }

    public void Open()
    {
        _step = Step.Menu;
        _status = null;
        _bt ??= new BluetoothTransport();
        _bt.Error += m => { _status = m; _rejoining = false; };
        _bt.JoinedHost += OnJoinedHost;
        BluetoothTransport.RequestPermissions(ok =>
        {
            _permissionsOk = ok;
            if (!ok) _status = Text.BluetoothPermissionsNeeded;
        });
    }

    public void Update() => _bt?.Poll();

    private PlayerInfo Me() => new PlayerInfo
    {
        Nickname = _profile.Nickname,
        Avatar = _profile.Avatar,
        Jewels = _wallet.Jewels,
        Inventory = _profile.Inventory,
    };

    private void Host()
    {
        if (!_bt.StartHosting())
        {
            _status = Text.CouldNotHost;
            return;
        }
        _bt.RequestDiscoverable();
        _host = new LobbyHost(_bt, Me(), new RoundSettings());
        _step = Step.Hosting;
        _status = Text.WaitingForPlayers;
    }

    private void Scan()
    {
        _bt.StartScan();
        _step = Step.Scanning;
        _status = null;
    }

    private void OnJoinedHost(IGuestTransport link)
    {
        _guestLink = link;
        if (_rejoining && _roundGuest != null)
        {
            _rejoining = false;
            _roundGuest.Rejoin(link);
            _status = null;
            return;
        }
        _guest = new LobbyGuest(link, Me());
        _guest.Other += m =>
        {
            if (Protocol.TypeOf(m) != MessageType.Start) return;
            _guest.Detach();
            var roundGuest = new RoundGuest(_guestLink, m);
            _roundGuest = roundGuest;
            PayFee(roundGuest.Settings.EntryFee);
            _round = RoundView.ForGuest(roundGuest, _profile);
            _round.Ended += Settle;
            _step = Step.Playing;
            _status = null;
        };
        _guest.Changed += () =>
        {
            if (_guest.Rejection != null) _status = _guest.Rejection;
            else if (!_guest.Connected) _status = Text.ConnectionLost;
        };
        _step = Step.InRoom;
    }

    /// <summary>The host starts the round: the lobby hands its players and links to the round host.</summary>
    private void StartRound()
    {
        _host.UpdateHostJewels(_wallet.Jewels);
        if (!_host.CanStart)
        {
            _status = Text.SomeoneCannotPay;
            return;
        }
        _host.Detach();
        ulong seed = (ulong)DateTime.UtcNow.Ticks;
        var roundHost = new RoundHost(_bt, _host.Players, _host.GuestPeers, _host.Settings, seed);
        PayFee(_host.Settings.EntryFee);
        _round = RoundView.ForHost(roundHost, _profile);
        _round.Ended += Settle;
        _step = Step.Playing;
        _status = null;
    }

    /// <summary>Every player, host included, pays the fee when the round starts.</summary>
    private void PayFee(long fee)
    {
        _fee = _wallet.TrySpendJewels(fee) ? fee : 0;
        _payout = 0;
        // Bought power-ups were sent with this player's Hello and are now in play on the host.
        _profile.Inventory = default;
        ProfileStore.Save(_profile);
    }

    /// <summary>This phone settles its own wallet: the winner takes the pot, a draw refunds, a lost host refunds nobody.</summary>
    private void Settle()
    {
        _payout = EntryFees.Payout(_round.Winner, _round.YourIndex, _fee, _round.Players.Count, _round.HostLost);
        if (_payout > 0) _wallet.AddJewels(_payout);
    }

    private void Leave()
    {
        if (_round != null) UnityEngine.Object.Destroy(_round.gameObject);
        _round = null;
        _roundGuest = null;
        _rejoining = false;
        _host?.Detach();
        _host = null;
        _guest?.Leave();
        _guest?.Detach();
        _guest = null;
        _bt?.StopScan();
        _bt?.StopHosting();
        _bt?.Dispose();
        _bt = null;
        Exit?.Invoke();
    }

    // ---------------- drawing ----------------

    public void OnGUI()
    {
        float w = Screen.width, u = Ui.U;
        if (_step == Step.Playing)
        {
            if (_round != null && _round.Over) DrawResult();
            else if (_roundGuest != null && _roundGuest.LinkLost) DrawLinkLost();
            return;
        }
        GUI.Label(new Rect(0, 3 * u, w, 10 * u), Text.Bluetooth, Ui.Title);

        if (_bt == null || !_bt.Available)
        {
            GUI.Label(new Rect(w * 0.15f, 30 * u, w * 0.7f, 12 * u), Text.BluetoothUnavailable, Ui.Label);
        }
        else if (!_bt.Enabled)
        {
            GUI.Label(new Rect(w * 0.15f, 25 * u, w * 0.7f, 10 * u), Text.BluetoothOff, Ui.Label);
            if (GUI.Button(new Rect(w * 0.35f, 38 * u, w * 0.3f, 11 * u), Text.TurnOnBluetooth, Ui.Button)) _bt.RequestEnable();
        }
        else
        {
            switch (_step)
            {
                case Step.Menu: DrawMenu(); break;
                case Step.Hosting: DrawRoom(_host.Players, _host.Settings, isHost: true); break;
                case Step.Scanning: DrawScan(); break;
                case Step.Joining: GUI.Label(new Rect(0, 30 * u, w, 10 * u), Text.Connecting, Ui.Label); break;
                case Step.InRoom:
                    if (_guest != null && _guest.YourIndex >= 0) DrawRoom(_guest.Players, _guest.Settings, isHost: false);
                    else GUI.Label(new Rect(0, 30 * u, w, 10 * u), Text.Connecting, Ui.Label);
                    break;
            }
        }

        if (_status != null) GUI.Label(new Rect(w * 0.1f, 78 * u, w * 0.8f, 8 * u), _status, Ui.Small);
        if (GUI.Button(new Rect(w * 0.4f, 87 * u, w * 0.2f, 10 * u), Text.Leave, Ui.Button)) Leave();
    }

    private void DrawMenu()
    {
        float w = Screen.width, u = Ui.U;
        GUI.enabled = _permissionsOk;
        if (GUI.Button(new Rect(w * 0.3f, 25 * u, w * 0.4f, 12 * u), Text.HostRoom, Ui.Button)) Host();
        if (GUI.Button(new Rect(w * 0.3f, 41 * u, w * 0.4f, 12 * u), Text.JoinRoom, Ui.Button)) Scan();
        GUI.enabled = true;
    }

    private void DrawScan()
    {
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 14 * u, w, 7 * u), _bt.Scanning ? Text.LookingForRooms : Text.NearbyPhones, Ui.Label);
        float y = 22 * u;
        // Phones confirmed to run Bomb Arena first, then paired phones, then the rest.
        var phones = _bt.Nearby.FindAll(p => p.RunsBombArena || p.Paired);
        phones.Sort((a, b) => (b.RunsBombArena ? 2 : 0) + (b.Paired ? 1 : 0) - ((a.RunsBombArena ? 2 : 0) + (a.Paired ? 1 : 0)));
        foreach (var p in phones)
        {
            if (y > 66 * u) break;
            string label = p.Name + (p.RunsBombArena ? "  - " + Text.GameTitle : "") + (p.Paired ? "  (" + Text.Paired + ")" : "");
            if (GUI.Button(new Rect(w * 0.2f, y, w * 0.6f, 9 * u), label, Ui.Button))
            {
                _hostAddress = p.Address;
                _bt.Connect(p.Address);
                _step = Step.Joining;
            }
            y += 10 * u;
        }
        if (phones.Count == 0 && !_bt.Scanning) GUI.Label(new Rect(0, 30 * u, w, 8 * u), Text.NoRoomsFound, Ui.Label);
        if (!_bt.Scanning && GUI.Button(new Rect(w * 0.4f, 68 * u, w * 0.2f, 9 * u), Text.ScanAgain, Ui.Button)) _bt.StartScan();
    }

    /// <summary>The room: every player's avatar and nickname, and the host's settings.</summary>
    private void DrawRoom(System.Collections.Generic.IReadOnlyList<PlayerInfo> players, RoundSettings settings, bool isHost)
    {
        float w = Screen.width, u = Ui.U;
        GUI.Label(new Rect(0, 13 * u, w, 7 * u), isHost ? Text.YourRoom : Text.InRoom, Ui.Label);
        for (int i = 0; i < players.Count; i++)
        {
            var r = new Rect(w * 0.25f, (22 + i * 13) * u, w * 0.5f, 12 * u);
            GUI.Box(r, GUIContent.none);
            GUI.DrawTexture(new Rect(r.x + u, r.y + u, 10 * u, 10 * u), PlaceholderSprites.Avatar(players[i].Avatar).texture, ScaleMode.ScaleToFit);
            // The bomber this player controls in the round.
            GUI.DrawTexture(new Rect(r.xMax - 9 * u, r.y + 2 * u, 8 * u, 8 * u), PlaceholderSprites.Bomber(i).texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x + 13 * u, r.y, r.width - 23 * u, r.height), players[i].Nickname + (i == 0 ? "  (" + Text.HostLabel + ")" : ""), Ui.Label);
        }
        GUI.Label(new Rect(0, 61 * u, w, 6 * u), Text.PlayersInRoom(players.Count, LobbyHost.MaxGuests + 1), Ui.Small);

        // Settings: the host changes them, guests see them.
        float y = 67 * u, bw = w * 0.17f;
        GUI.enabled = isHost;
        if (GUI.Button(new Rect(w * 0.08f, y, bw, 8 * u), Text.ArenaSize(settings.Width, settings.Height), Ui.SmallButton) && isHost)
            _host.ChangeSettings(NextSize(settings));
        if (GUI.Button(new Rect(w * 0.08f + bw + u, y, bw, 8 * u), Text.RoundTime(settings.TimeLimitSeconds), Ui.SmallButton) && isHost)
            _host.ChangeSettings(NextTime(settings));
        if (GUI.Button(new Rect(w * 0.08f + 2 * (bw + u), y, bw * 0.7f, 8 * u), Text.EntryFee(settings.EntryFee), Ui.SmallButton) && isHost)
            _host.ChangeSettings(NextFee(settings));
        if (GUI.Button(new Rect(w * 0.08f + 2.7f * bw + 3 * u, y, bw * 0.75f, 8 * u), Text.Enemies(settings.EnemiesOn), Ui.SmallButton) && isHost)
        {
            var n = settings.Clone();
            n.EnemiesOn = !n.EnemiesOn;
            _host.ChangeSettings(n);
        }
        GUI.enabled = true;

        if (isHost)
        {
            _host.UpdateHostJewels(_wallet.Jewels);
            GUI.enabled = _host.CanStart;
            if (GUI.Button(new Rect(w * 0.72f, y, w * 0.2f, 8 * u), Text.StartRound, Ui.Button)) StartRound();
            GUI.enabled = true;
            if (players.Count < 2) _status = Text.NeedAnotherPlayer;
            else if (!_host.CanStart) _status = Text.SomeoneCannotPay;
            else if (_status == Text.NeedAnotherPlayer || _status == Text.WaitingForPlayers || _status == Text.SomeoneCannotPay) _status = null;
        }
    }

    // Arena sizes cycle through odd widths 19–27 and odd heights 9–15.
    private static RoundSettings NextSize(RoundSettings s)
    {
        var n = s.Clone();
        n.Height += 2;
        if (n.Height > Arena.MaxHeight)
        {
            n.Height = Arena.MinHeight;
            n.Width = n.Width + 2 > Arena.MaxWidth ? Arena.MinWidth : n.Width + 2;
        }
        return n;
    }

    private static RoundSettings NextFee(RoundSettings s)
    {
        var n = s.Clone();
        int i = Array.IndexOf(RoundSettings.EntryFeeChoices, s.EntryFee);
        n.EntryFee = RoundSettings.EntryFeeChoices[(i + 1) % RoundSettings.EntryFeeChoices.Length];
        return n;
    }

    private static RoundSettings NextTime(RoundSettings s)
    {
        var n = s.Clone();
        int i = Array.IndexOf(RoundSettings.TimeLimitChoices, s.TimeLimitSeconds);
        n.TimeLimitSeconds = RoundSettings.TimeLimitChoices[(i + 1) % RoundSettings.TimeLimitChoices.Length];
        return n;
    }

    /// <summary>A guest's link dropped mid-round: reconnect to the same host, or give up (no refund).</summary>
    private void DrawLinkLost()
    {
        var col = new Ui.Column(Ui.Panel(0.55f, 0.6f));
        GUI.Label(col.Next(10), Text.LinkLostTitle, Ui.Title);
        GUI.Label(col.Next(12), _rejoining ? Text.Reconnecting : Text.ReconnectHint, Ui.Small);
        GUI.enabled = !_rejoining && _hostAddress != null;
        if (GUI.Button(col.Next(10), Text.Reconnect, Ui.Button))
        {
            _rejoining = true;
            _bt.Connect(_hostAddress);
        }
        GUI.enabled = true;
        if (GUI.Button(col.Next(10), Text.Leave, Ui.Button)) Leave();
    }

    private void DrawResult()
    {
        var col = new Ui.Column(Ui.Panel(0.5f, 0.6f));
        string title = _round.HostLost ? Text.HostLeft
            : _round.Winner is int w ? (w == _round.YourIndex ? Text.YouWin : Text.Winner(_round.Players[w].Nickname))
            : Text.Draw;
        GUI.Label(col.Next(_round.HostLost ? 16 : 12), title, _round.HostLost ? Ui.Label : Ui.Title);
        string money = _round.HostLost ? Text.NoRefund
            : _round.Winner == null ? Text.FeesRefunded
            : Text.PotWon(EntryFees.Pot(_fee, _round.Players.Count));
        GUI.Label(col.Next(7), money, Ui.Label);
        GUI.Label(col.Next(7), Text.JewelChange(_payout - _fee), Ui.Small);
        if (GUI.Button(col.Next(11), Text.BackToMenu, Ui.Button)) Leave();
    }
}
