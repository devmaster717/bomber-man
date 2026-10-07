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
    private GameSounds _sounds;
    private bool _threeD;
    private ArenaTheme _theme;
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
    /// Settings that matter in play: the control layout (joystick or D-pad, button size, pad side), the 2D or 3D view
    /// and the arena's theme; the view and theme can be switched mid-attempt from the pause menu.
    /// </summary>
    public void ApplySettings(PlayerProfile settings)
    {
        _controls.UseJoystick = settings.UseJoystick;
        _controls.PadOnRight = settings.PadOnRight;
        _controls.Scale = settings.ButtonScalePercent / 100f;
        var theme = ArenaTheme.At(settings.Arena);
        if (_renderer == null || _threeD != settings.View3D || _theme != theme)
        {
            _renderer?.Destroy();
            _threeD = settings.View3D;
            _theme = theme;
            _renderer = ArenaView.Create(Game, _threeD, _theme);
            _renderer.Follow(0);
        }
    }

    // Sounds and vibration follow what happens in the core; the core knows nothing about them.
    private void HookFeedback()
    {
        _sounds = new GameSounds(Game, 0);
        Finished += g =>
        {
            if (Feedback.Instance == null) return;
            if (g.Outcome == Outcome.Cleared) Feedback.Instance.StageClear();
            else Feedback.Instance.StageFailed();
        };
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
            _sounds.Observe();
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
        GUI.Label(new Rect(Ui.W * 0.03f, Ui.H * 0.015f, Ui.W * 0.85f, Ui.H * 0.12f), hud, Ui.Hud);
    }

    private int StageNumber => int.TryParse(name.Replace("Stage ", ""), out int n) ? n : 0;
}
