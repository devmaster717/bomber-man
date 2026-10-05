using BombArena.Core;
using UnityEngine;

/// <summary>
/// Runs the game core on its fixed 20-tick clock, feeds it input and draws its state. Holds no rules of its
/// own (ADR 0001).
/// </summary>
public sealed class GameView : MonoBehaviour
{
    [SerializeField] private int width = Arena.DefaultWidth;
    [SerializeField] private int height = Arena.DefaultHeight;
    [SerializeField] private int seed = 1;
    [SerializeField, Range(0, 100)] private int softBlockPercent = Arena.DefaultSoftBlockPercent;
    [SerializeField] private int walkers = 4;

    /// <summary>Most tiles shown vertically; larger arenas scroll.</summary>
    private const float MaxVisibleTilesHigh = 11f;
    private const float TickSeconds = 1f / Units.TicksPerSecond;

    private Game _game;
    private ArenaRenderer _renderer;
    private TouchControls _controls;
    private Camera _camera;
    private float _accumulator;
    private bool _bombQueued, _detonateQueued;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<GameView>() == null)
            new GameObject("Bomb Arena").AddComponent<GameView>();
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        _controls = gameObject.AddComponent<TouchControls>();
        StartAttempt();
    }

    private void StartAttempt()
    {
        _renderer?.Destroy();
        _game = Game.Create(width, height, (ulong)seed, softBlockPercent, walkers);
        _renderer = new ArenaRenderer(_game);
        _accumulator = 0f;
        _bombQueued = _detonateQueued = false;
        SetUpCamera();
        FollowCamera(_renderer.BomberDrawPosition(0));
    }

    private void Update()
    {
        // Presses are latched until the next tick consumes them, so a quick tap is never lost.
        _bombQueued |= _controls.BombPressed || Input.GetKeyDown(KeyCode.Space);
        _detonateQueued |= _controls.DetonatePressed || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.E);

        if (_game.Outcome == Outcome.Playing)
        {
            var move = ReadDirection();
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, 0.25f);
            while (_accumulator >= TickSeconds && _game.Outcome == Outcome.Playing)
            {
                _game.Step(new BomberInput(move, _bombQueued, _detonateQueued));
                _bombQueued = _detonateQueued = false;
                _renderer.OnTick();
                _accumulator -= TickSeconds;
            }
        }
        else if (_bombQueued || Input.GetKeyDown(KeyCode.Return))
        {
            StartAttempt();
            return;
        }

        _renderer.Draw(_accumulator / TickSeconds);
        FollowCamera(_renderer.BomberDrawPosition(0));
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

    private GUIStyle _banner, _hud;

    private void OnGUI()
    {
        if (_game == null) return;
        _hud ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        _hud.fontSize = (int)(Screen.height * 0.04f);
        GUI.Label(new Rect(Screen.width * 0.02f, Screen.height * 0.01f, Screen.width * 0.5f, Screen.height * 0.06f),
            $"Enemies: {_game.EnemiesRemaining}", _hud);

        if (_game.Outcome == Outcome.Playing) return;
        _banner ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _banner.fontSize = (int)(Screen.height * 0.06f);
        float w = Screen.width * 0.5f, h = Screen.height * 0.22f;
        string text = _game.Outcome == Outcome.Failed ? "You died\nPress BOMB to restart" : "Stage clear!";
        GUI.Box(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), text, _banner);
    }

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
        float tilesHigh = Mathf.Min(MaxVisibleTilesHigh, _game.Arena.Height, _game.Arena.Width / _camera.aspect);
        _camera.orthographicSize = tilesHigh / 2f;
    }

    private void FollowCamera(Vector2 target)
    {
        float halfH = _camera.orthographicSize, halfW = halfH * _camera.aspect;
        int w = _game.Arena.Width, h = _game.Arena.Height;

        float x = Clamp(target.x, -0.5f + halfW, w - 0.5f - halfW, (w - 1) / 2f);
        float y = Clamp(target.y, -(h - 0.5f) + halfH, 0.5f - halfH, -(h - 1) / 2f);
        _camera.transform.position = new Vector3(x, y, -10f);
    }

    private static float Clamp(float value, float min, float max, float centreIfTooSmall) =>
        min > max ? centreIfTooSmall : Mathf.Clamp(value, min, max);
}
