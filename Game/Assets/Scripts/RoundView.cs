using System;
using System.Collections.Generic;
using BombArena.Core;
using BombArena.Core.Net;
using UnityEngine;

/// <summary>
/// Plays a Bluetooth round on this phone. On the host it runs the round (the only copy of the rules) at 20 ticks a
/// second; on a guest it sends the player's controls and draws the state the host sends back, smoothing movement
/// between updates. Each phone's camera follows its own bomber.
/// </summary>
public sealed class RoundView : MonoBehaviour
{
    private const float TickSeconds = 1f / Units.TicksPerSecond;

    private RoundHost _host;
    private RoundGuest _guest;
    private IArenaView _renderer;
    private GameSounds _sounds;
    private bool _threeD;
    private ArenaTheme _theme;
    private TouchControls _controls;
    private float _accumulator, _sinceUpdate;
    private bool _bombQueued, _detonateQueued;

    public Game Game => _host != null ? _host.Game : _guest.Game;
    public int YourIndex => _host != null ? 0 : _guest.YourIndex;
    public IReadOnlyList<PlayerInfo> Players => _host != null ? _host.Players : (IReadOnlyList<PlayerInfo>)_guest.Players;

    public RoundHost Host => _host;
    public RoundGuest Guest => _guest;

    /// <summary>True once the round has a result.</summary>
    public bool Over => _host != null ? Game.Outcome == Outcome.RoundOver : _guest.Finished;

    /// <summary>True while the round is paused, waiting for a player (or for the host).</summary>
    public bool Paused => _host != null ? _host.Paused : _guest.PausedFor != null || _guest.LinkLost;

    public int? Winner => _host != null ? Game.Winner : _guest.Winner;
    public bool HostLost => _guest != null && _guest.HostLost;

    /// <summary>Raised once when the round is over.</summary>
    public event Action Ended;
    private bool _endedRaised;

    public static RoundView ForHost(RoundHost host, PlayerProfile settings)
    {
        var v = Create(settings);
        v._host = host;
        v._host.Clock = () => Time.realtimeSinceStartup;
        v.Build();
        return v;
    }

    public static RoundView ForGuest(RoundGuest guest, PlayerProfile settings)
    {
        var v = Create(settings);
        v._guest = guest;
        v._guest.Updated += v.OnSnapshot;
        v.Build();
        return v;
    }

    private static RoundView Create(PlayerProfile settings)
    {
        var v = new GameObject("Round").AddComponent<RoundView>();
        v._controls = v.gameObject.AddComponent<TouchControls>();
        v._controls.UseJoystick = settings.UseJoystick;
        v._controls.PadOnRight = settings.PadOnRight;
        v._controls.Scale = settings.ButtonScalePercent / 100f;
        v._threeD = settings.View3D;
        v._theme = ArenaTheme.At(settings.Arena);
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // no dimming mid-round
        return v;
    }

    private void Build()
    {
        _renderer = ArenaView.Create(Game, _threeD, _theme);
        _renderer.Follow(YourIndex);
        _sounds = new GameSounds(Game, YourIndex);
    }

    private void OnDestroy()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
        _renderer?.Destroy();
        if (_guest != null) _guest.Updated -= OnSnapshot;
    }

    private void OnSnapshot()
    {
        _renderer.OnTick();
        _sounds?.Observe();
        _sinceUpdate = 0f;
    }

    private void Update()
    {
        var me = Game.Bombers[YourIndex];
        _controls.enabled = !Over && !Paused && me.Alive;
        _controls.ShowDetonate = me.HasRemoteControl;

        _bombQueued |= _controls.BombPressed || Input.GetKeyDown(KeyCode.Space);
        _detonateQueued |= _controls.DetonatePressed || Input.GetKeyDown(KeyCode.E);
        var move = _controls.enabled ? ReadDirection() : Direction.None;

        if (_host != null)
        {
            _host.SetHostInput(move, _bombQueued, _detonateQueued);
            _bombQueued = _detonateQueued = false;
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, 0.25f);
            while (_accumulator >= TickSeconds && !Over)
            {
                _host.Tick();
                _renderer.OnTick();
                _sounds.Observe();
                _accumulator -= TickSeconds;
            }
            _renderer.Draw(_accumulator / TickSeconds);
        }
        else
        {
            _guest.SendInput(move, _bombQueued, _detonateQueued);
            _bombQueued = _detonateQueued = false;
            // Snapshots arrive about every 50 ms; draw smoothly between the last two.
            _sinceUpdate += Time.deltaTime;
            _renderer.Draw(Mathf.Clamp01(_sinceUpdate / TickSeconds));
        }

        _renderer.Follow(YourIndex);

        if (Over && !_endedRaised)
        {
            _endedRaised = true;
            if (Feedback.Instance != null)
            {
                if (Winner == YourIndex) Feedback.Instance.StageClear();
                else Feedback.Instance.StageFailed();
            }
            Ended?.Invoke();
        }
    }

    // Leaving the app pauses the round for everyone until this player is back.
    private void OnApplicationPause(bool paused)
    {
        if (_host != null) _host.SetHostAway(paused);
        else _guest.SetAway(paused);
    }

    private void Forfeit()
    {
        if (_host != null) _host.ForfeitHost();
        else _guest.Forfeit();
    }

    private Direction ReadDirection()
    {
        if (_controls.Held != Direction.None) return _controls.Held;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) return Direction.Up;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) return Direction.Down;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) return Direction.Left;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) return Direction.Right;
        return Direction.None;
    }

    private void OnGUI()
    {
        Ui.Begin();
        Ui.HudBar(0.14f);
        // Each player's avatar, their bomber, nickname and whether they are still in.
        float u = Ui.U, x = 2 * u;
        for (int i = 0; i < Players.Count; i++)
        {
            bool alive = Game.Bombers[i].Alive;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alive ? 1f : 0.4f);
            GUI.DrawTexture(new Rect(x, u, 7 * u, 7 * u), PlayerAvatars.Portrait(Players[i].Avatar), ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(x + 7.5f * u, 2 * u, 5 * u, 5 * u), PalaceSprites.Bomber(i).texture, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x + 13 * u, u, 26 * u, 7 * u), Players[i].Nickname + (alive ? "" : "  " + Text.Out), Ui.Small);
            GUI.color = old;
            x += 40 * u;
        }
        // Forfeit: leave the round on purpose (the fee stays in the pot).
        if (!Over && Game.Bombers[YourIndex].Alive &&
            GUI.Button(new Rect(Ui.W - 24 * u, 10 * u, 22 * u, 8 * u), Text.ForfeitButton, Ui.SmallButton))
            Forfeit();

        int? waitingFor = _host != null ? (_host.WaitingFor) : _guest.PausedFor;
        if (waitingFor is int w && !Over && !(_guest != null && _guest.LinkLost))
        {
            var col = new Ui.Column(Ui.Panel(0.5f, 0.4f));
            int seconds = _host != null ? (int)_host.WaitedSeconds : 0;
            GUI.Label(col.Next(10), Text.WaitingFor(Players[w].Nickname, seconds), Ui.Label);
            if (_host != null && _host.CanForfeitMissing && GUI.Button(col.Next(10), Text.CountAsForfeit, Ui.Button))
                _host.ForfeitMissing();
        }

        if (Game.TimeLimitTicks > 0)
        {
            long left = Math.Max(0, (Game.TimeLimitTicks - Game.Tick) / Units.TicksPerSecond);
            GUI.Label(new Rect(Ui.W - 30 * u, u, 28 * u, 7 * u), Ui.Clock(left), Ui.Label);
        }
    }
}
