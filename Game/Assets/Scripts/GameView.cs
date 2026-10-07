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
        string hud = Text.Hud(StageNumber, Game.EnemiesRemaining, Ui.Clock(elapsed));
        var b = Game.Bomber;
        string held = (b.HasFireUp ? "  " + Text.HeldFireUp : "") + (b.BombUps > 0 ? "  " + Text.HeldBombs(b.MaxBombs) : "") +
                      (b.HasRemoteControl ? "  " + Text.HeldRemote : "") +
                      (b.SpeedUpTicksLeft > 0 ? "  " + Text.HeldSpeed(Mathf.CeilToInt(b.SpeedUpTicksLeft / (float)Units.TicksPerSecond)) : "");
        if (held.Length > 0) hud += Environment.NewLine + held.Trim();
        GUI.Label(new Rect(Ui.W * 0.03f, Ui.H * 0.015f, Ui.W * 0.85f, Ui.H * 0.12f), hud, Ui.Hud);
        DrawStarMeter();
    }

    // The stars still within reach: a meter draining over the stage's whole time (3T), notched where 3 stars drop to
    // 2 (at T) and 2 to 1 (at 1.5T), beside three stars that empty as they're lost, and the time left at this rating.
    private int _starsSeen = 3, _lostStar = -1;
    private float _starLostAt = -10f;
    private GUIStyle _meterClock;

    private void DrawStarMeter()
    {
        long target = Game.TargetTicks;
        if (target <= 0) return;
        long t = Game.ClearedOnTick ?? Game.Tick, end = 3 * target;
        int stars = t < target ? 3 : 2 * t < 3 * target ? 2 : 1;
        long nextDrop = stars == 3 ? target : stars == 2 ? (3 * target + 1) / 2 : end;
        if (stars < _starsSeen)
        {
            _lostStar = stars; // the slot that has just emptied
            _starLostAt = Time.unscaledTime;
        }
        _starsSeen = stars;

        float u = Ui.U, right = Ui.W - 14f * u, y = 2.4f * u;
        _meterClock ??= new GUIStyle(Ui.Numbers) { fontSize = (int)(3.6f * u), alignment = TextAnchor.MiddleRight };
        const float clockW = 7f;
        float barW = Ui.W * 0.2f, star = 4.6f * u, gap = 0.5f * u;
        var bar = new Rect(right - clockW * u - u - barW, y, barW, 3.4f * u);
        float x = bar.x - u - 3 * star - 2 * gap;
        for (int i = 0; i < 3; i++)
        {
            var slot = new Rect(x + i * (star + gap), bar.center.y - star / 2f, star, star);
            GUI.DrawTexture(slot, Ui.Star(i < stars), ScaleMode.ScaleToFit);
            // The star just lost swells and fades away from its slot.
            float p = (Time.unscaledTime - _starLostAt) / 0.6f;
            if (i == _lostStar && p < 1f)
            {
                var matrix = GUI.matrix;
                var old = GUI.color;
                GUIUtility.ScaleAroundPivot(Vector2.one * (1f + 0.7f * p), slot.center + new Vector2(GUI.matrix.m03, GUI.matrix.m13));
                GUI.color = new Color(1f, 1f, 1f, 1f - p);
                GUI.DrawTexture(slot, Ui.Star(true), ScaleMode.ScaleToFit);
                GUI.color = old;
                GUI.matrix = matrix;
            }
        }

        var tint = stars == 3 ? new Color(1f, 0.83f, 0.36f) : stars == 2 ? new Color(1f, 0.6f, 0.22f) : new Color(0.93f, 0.27f, 0.2f);
        Ui.Meter(bar, 1f - t / (float)end, tint, 2f / 3f, 0.5f);
        long left = Math.Max(0, (nextDrop - t + Units.TicksPerSecond - 1) / Units.TicksPerSecond);
        GUI.Label(new Rect(bar.xMax + u, bar.y - u, clockW * u, bar.height + 2 * u), Ui.Clock(left), _meterClock);
    }

    private int StageNumber => int.TryParse(name.Replace("Stage ", ""), out int n) ? n : 0;
}
