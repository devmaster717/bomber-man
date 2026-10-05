using System;
using BombArena.Core;
using UnityEngine;

/// <summary>
/// Runs one stage attempt: steps the game core on its fixed 20-tick clock, feeds it input and draws its state.
/// Holds no rules of its own (ADR 0001). Screens around it (home, pause, results) belong to <see cref="AppController"/>.
/// </summary>
public sealed class GameView : MonoBehaviour
{
    /// <summary>Most tiles shown vertically; larger arenas scroll.</summary>
    private const float MaxVisibleTilesHigh = 11f;
    private const float TickSeconds = 1f / Units.TicksPerSecond;

    public Game Game { get; private set; }

    /// <summary>While paused the clock stops and input is ignored.</summary>
    public bool Paused { get; set; }

    /// <summary>Raised once when the attempt is cleared or failed.</summary>
    public event Action<Game> Finished;

    private ArenaRenderer _renderer;
    private TouchControls _controls;
    private Camera _camera;
    private float _accumulator;
    private bool _bombQueued, _detonateQueued, _finishedRaised;

    public static GameView Begin(StageSpec spec, ulong attemptSeed, PowerUpLoadout startWith)
    {
        var view = new GameObject("Stage " + spec.Number).AddComponent<GameView>();
        view.Game = Game.ForStage(spec, attemptSeed);
        view.Game.ApplyLoadout(0, startWith);
        view._controls = view.gameObject.AddComponent<TouchControls>();
        view._renderer = new ArenaRenderer(view.Game);
        view.SetUpCamera();
        view.FollowCamera(view._renderer.BomberDrawPosition(0));
        return view;
    }

    private void OnDestroy() => _renderer?.Destroy();

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
        FollowCamera(_renderer.BomberDrawPosition(0));

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

    private GUIStyle _hud;

    private void OnGUI()
    {
        if (Game == null) return;
        _hud ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        _hud.fontSize = (int)(Screen.height * 0.04f);
        long elapsed = Game.Tick / Units.TicksPerSecond;
        long target = Game.TargetTicks / Units.TicksPerSecond;
        string hud = $"Stage {StageNumber}    Enemies: {Game.EnemiesRemaining}    Time {Ui.Clock(elapsed)}";
        if (target > 0) hud += $"    3 stars under {Ui.Clock(target)}";
        var b = Game.Bomber;
        string held = (b.HasFireUp ? "  Fire Up" : "") + (b.BombUps > 0 ? $"  Bombs {b.MaxBombs}" : "") +
                      (b.HasRemoteControl ? "  Remote" : "") +
                      (b.SpeedUpTicksLeft > 0 ? $"  Speed {Mathf.CeilToInt(b.SpeedUpTicksLeft / (float)Units.TicksPerSecond)}s" : "");
        if (held.Length > 0) hud += Environment.NewLine + held.Trim();
        GUI.Label(new Rect(Screen.width * 0.02f, Screen.height * 0.01f, Screen.width * 0.85f, Screen.height * 0.12f), hud, _hud);
    }

    private int StageNumber => int.TryParse(name.Replace("Stage ", ""), out int n) ? n : 0;

    private void SetUpCamera()
    {
        _camera = Camera.main;
        if (_camera == null)
        {
            _camera = new GameObject("Main Camera").AddComponent<Camera>();
            _camera.tag = "MainCamera";
        }
        _camera.orthographic = true;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;

        // Never show space outside the walls: the view is no taller or wider than the arena.
        float tilesHigh = Mathf.Min(MaxVisibleTilesHigh, Game.Arena.Height, Game.Arena.Width / _camera.aspect);
        _camera.orthographicSize = tilesHigh / 2f;
    }

    private void FollowCamera(Vector2 target)
    {
        float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
        int w = Game.Arena.Width, h = Game.Arena.Height;

        float x = Clamp(target.x, -0.5f + halfW, w - 0.5f - halfW, (w - 1) / 2f);
        float y = Clamp(target.y, -(h - 0.5f) + halfH, 0.5f - halfH, -(h - 1) / 2f);
        _camera.transform.position = new Vector3(x, y, -10f);
    }

    private static float Clamp(float value, float min, float max, float centreIfTooSmall) =>
        min > max ? centreIfTooSmall : Mathf.Clamp(value, min, max);
}
