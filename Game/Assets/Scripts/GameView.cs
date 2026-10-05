using BombArena.Core;
using UnityEngine;

/// <summary>
/// Runs the game core on its fixed 20-tick clock and draws its state. Holds no rules of its own:
/// it only reads input, steps the core and renders (ADR 0001).
/// </summary>
public sealed class GameView : MonoBehaviour
{
    [SerializeField] private int width = Arena.DefaultWidth;
    [SerializeField] private int height = Arena.DefaultHeight;
    [SerializeField] private int seed = 1;
    [SerializeField, Range(0, 100)] private int softBlockPercent = Arena.DefaultSoftBlockPercent;

    /// <summary>Most tiles shown vertically; larger arenas scroll.</summary>
    private const float MaxVisibleTilesHigh = 11f;
    private const float TickSeconds = 1f / Units.TicksPerSecond;

    private Game _game;
    private TouchDpad _dpad;
    private Camera _camera;
    private Transform _bomber;
    private Vector2 _previous, _current;
    private float _accumulator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<GameView>() == null)
            new GameObject("Bomb Arena").AddComponent<GameView>();
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        _game = Game.Create(width, height, (ulong)seed, softBlockPercent);
        _dpad = gameObject.AddComponent<TouchDpad>();

        DrawArena();
        _bomber = MakeSprite("Bomber", PlaceholderSprites.Bomber, 10).transform;
        _previous = _current = BomberWorldPosition();
        _bomber.position = _current;

        SetUpCamera();
        FollowCamera(_current);
    }

    private void Update()
    {
        var input = ReadInput();
        _accumulator = Mathf.Min(_accumulator + Time.deltaTime, 0.25f);
        while (_accumulator >= TickSeconds)
        {
            _previous = _current;
            _game.Step(input);
            _current = BomberWorldPosition();
            _accumulator -= TickSeconds;
        }

        var drawn = Vector2.Lerp(_previous, _current, _accumulator / TickSeconds);
        _bomber.position = drawn;
        FollowCamera(drawn);
    }

    private Direction ReadInput()
    {
        if (_dpad.Held != Direction.None) return _dpad.Held;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) return Direction.Up;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) return Direction.Down;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) return Direction.Left;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) return Direction.Right;
        return Direction.None;
    }

    // World space: one unit per tile, tile (x, y) centred at (x, -y) so row 0 is at the top.
    private Vector2 BomberWorldPosition() =>
        new Vector2(_game.Bomber.X / (float)Units.PerTile, -_game.Bomber.Y / (float)Units.PerTile);

    private void DrawArena()
    {
        var arena = _game.Arena;
        var parent = new GameObject("Arena").transform;
        for (int y = 0; y < arena.Height; y++)
        for (int x = 0; x < arena.Width; x++)
        {
            var tile = MakeSprite($"{arena[x, y]} ({x},{y})", PlaceholderSprites.For(arena[x, y]), 0);
            tile.transform.SetParent(parent);
            tile.transform.position = new Vector2(x, -y);
        }
    }

    private static GameObject MakeSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return go;
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
