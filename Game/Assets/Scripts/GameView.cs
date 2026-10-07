using System;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// Runs one stage attempt: steps the game core on its fixed 20-tick clock, feeds it input and draws its state.
/// Holds no rules of its own (ADR 0001). Screens around it (home, pause, results) belong to <see cref="AppController"/>.
/// </summary>
public sealed class GameView : MonoBehaviour
{
    private const float TickSeconds = 1f / Units.TicksPerSecond;

    public Game Game { get; private set; }

    /// <summary>While paused the clock stops and input is ignored.</summary>
    public bool Paused { get; set; }

    /// <summary>Raised once when the attempt is cleared or failed.</summary>
    public event Action<Game> Finished;

    private IArenaView _renderer;
    private bool _threeD;
    private TouchControls _controls;
    private float _accumulator;
    private bool _bombQueued, _detonateQueued, _finishedRaised;

    public static GameView Begin(StageSpec spec, ulong attemptSeed, PowerUpLoadout startWith, PlayerProfile settings)
    {
        var view = new GameObject("Stage " + spec.Number).AddComponent<GameView>();
        view.Game = Game.ForStage(spec, attemptSeed);
        view.Game.ApplyLoadout(0, startWith);
        view._controls = view.gameObject.AddComponent<TouchControls>();
        view.ApplySettings(settings);
        view.HookFeedback();
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // no dimming mid-stage
        return view;
    }

    private void OnDestroy()
    {
        _renderer?.Destroy();
        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }

    /// <summary>
    /// Settings that matter in play: the control layout (joystick or D-pad, button size, left-handed mirror) and the
    /// 2D or 3D view, which can be switched mid-attempt from the pause menu.
    /// </summary>
    public void ApplySettings(PlayerProfile settings)
    {
        _controls.UseJoystick = settings.UseJoystick;
        _controls.PadOnRight = settings.PadOnRight;
        _controls.Scale = settings.ButtonScalePercent / 100f;
        if (_renderer == null || _threeD != settings.View3D)
        {
            _renderer?.Destroy();
            _threeD = settings.View3D;
            _renderer = ArenaView.Create(Game, _threeD);
            _renderer.Follow(0);
        }
    }

    // Sounds and vibration follow what happens in the core; the core knows nothing about them.
    private void HookFeedback()
    {
        var fx = Feedback.Instance;
        if (fx == null) return;
        Game.BombPlaced += b => { if (b.Owner.Index == 0) fx.BombPlaced(); };
        Game.BombExploded += b =>
        {
            var me = Game.Bomber.Tile;
            fx.Explosion(nearby: Mathf.Abs(b.Tile.X - me.X) + Mathf.Abs(b.Tile.Y - me.Y) <= 3);
        };
        Game.BomberDied += b => { if (b.Index == 0) fx.Died(); };
        Game.PowerUpPicked += (b, k) => { if (b.Index == 0) fx.PowerUp(); };
        Finished += g => { if (g.Outcome == Outcome.Cleared) fx.StageClear(); };
    }

    private void Update()
    {
        _controls.enabled = !Paused && Game.Outcome == Outcome.Playing;
        _controls.ShowDetonate = Game.Bomber.HasRemoteControl;
        if (Paused || Game.Outcome != Outcome.Playing)
        {
            _renderer.Draw(1f);
            return;
        }

        // Presses are latched until the next tick consumes them, so a quick tap is never lost.
        _bombQueued |= _controls.BombPressed || Input.GetKeyDown(KeyCode.Space);
        _detonateQueued |= _controls.DetonatePressed || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.E);

        var move = ReadDirection();
        _accumulator = Mathf.Min(_accumulator + Time.deltaTime, 0.25f);
        while (_accumulator >= TickSeconds && Game.Outcome == Outcome.Playing)
        {
            Game.Step(new BomberInput(move, _bombQueued, _detonateQueued));
            _bombQueued = _detonateQueued = false;
            _renderer.OnTick();
            _accumulator -= TickSeconds;
        }

        _renderer.Draw(_accumulator / TickSeconds);
        _renderer.Follow(0);

        if (Game.Outcome != Outcome.Playing && !_finishedRaised)
        {
            _finishedRaised = true;
            Finished?.Invoke(Game);
        }
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
        if (Game == null) return;
        Ui.Begin();
        Ui.HudBar(0.17f);
        long elapsed = Game.Tick / Units.TicksPerSecond;
        long target = Game.TargetTicks / Units.TicksPerSecond;
        string hud = Text.Hud(StageNumber, Game.EnemiesRemaining, Ui.Clock(elapsed));
        if (target > 0) hud += Text.ThreeStarsUnder(Ui.Clock(target));
        var b = Game.Bomber;
        string held = (b.HasFireUp ? "  " + Text.HeldFireUp : "") + (b.BombUps > 0 ? "  " + Text.HeldBombs(b.MaxBombs) : "") +
                      (b.HasRemoteControl ? "  " + Text.HeldRemote : "") +
                      (b.SpeedUpTicksLeft > 0 ? "  " + Text.HeldSpeed(Mathf.CeilToInt(b.SpeedUpTicksLeft / (float)Units.TicksPerSecond)) : "");
        if (held.Length > 0) hud += Environment.NewLine + held.Trim();
        GUI.Label(new Rect(Screen.width * 0.03f, Screen.height * 0.015f, Screen.width * 0.85f, Screen.height * 0.12f), hud, Ui.Hud);
    }

    private int StageNumber => int.TryParse(name.Replace("Stage ", ""), out int n) ? n : 0;
}
