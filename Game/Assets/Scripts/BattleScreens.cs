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
    }

    private readonly PlayerProfile _profile;
    private readonly IWallet _wallet;
    private BluetoothTransport _bt;
    private LobbyHost _host;
    private LobbyGuest _guest;
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
        _bt.Error += m => _status = m;
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
        _guest = new LobbyGuest(link, Me());
        _guest.Changed += () =>
        {
            if (_guest.Rejection != null) _status = _guest.Rejection;
            else if (!_guest.Connected) _status = Text.ConnectionLost;
        };
        _step = Step.InRoom;
    }

    private void Leave()
    {
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
            GUI.DrawTexture(new Rect(r.x + u, r.y + u, 10 * u, 10 * u), PlaceholderSprites.BomberAvatar(players[i].Avatar).texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(r.x + 13 * u, r.y, r.width - 14 * u, r.height), players[i].Nickname + (i == 0 ? "  (" + Text.HostLabel + ")" : ""), Ui.Label);
        }
        GUI.Label(new Rect(0, 62 * u, w, 7 * u), Text.PlayersInRoom(players.Count, LobbyHost.MaxGuests + 1), Ui.Small);
    }
}
