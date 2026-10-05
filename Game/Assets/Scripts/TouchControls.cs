using BombArena.Core;
using UnityEngine;

/// <summary>
/// On-screen controls: a D-pad bottom-left, a large Bomb button bottom-right and a smaller Detonate button
/// above it (shown only when enabled). Reads every touch so the D-pad and buttons work together, plus the
/// mouse in the editor.
/// </summary>
public sealed class TouchControls : MonoBehaviour
{
    public Direction Held { get; private set; }

    /// <summary>True on the frame the Bomb button was pressed.</summary>
    public bool BombPressed { get; private set; }

    /// <summary>True on the frame the Detonate button was pressed.</summary>
    public bool DetonatePressed { get; private set; }

    /// <summary>Whether the Detonate button is shown (while the bomber holds Remote Control).</summary>
    public bool ShowDetonate { get; set; }

    private float Unit => Screen.height * 0.14f;
    private float Margin => Screen.height * 0.05f;

    private bool _bombWasDown, _detonateWasDown;

    // Rects in GUI space (origin top-left).
    private Rect DpadRect(Direction d)
    {
        float s = Unit, m = Margin;
        float cx = m + s, cy = Screen.height - m - 2 * s;
        return d switch
        {
            Direction.Up => new Rect(cx, cy - s, s, s),
            Direction.Down => new Rect(cx, cy + s, s, s),
            Direction.Left => new Rect(cx - s, cy, s, s),
            Direction.Right => new Rect(cx + s, cy, s, s),
            _ => Rect.zero,
        };
    }

    private Rect BombRect
    {
        get
        {
            float size = Unit * 1.6f;
            return new Rect(Screen.width - Margin - size, Screen.height - Margin - size, size, size);
        }
    }

    private Rect DetonateRect
    {
        get
        {
            float size = Unit;
            var bomb = BombRect;
            return new Rect(bomb.center.x - size / 2, bomb.y - Margin * 0.5f - size, size, size);
        }
    }

    private void Update()
    {
        Held = Direction.None;
        bool bombDown = false, detonateDown = false;

        void Consider(Vector2 screenPos)
        {
            var p = new Vector2(screenPos.x, Screen.height - screenPos.y);
            if (BombRect.Contains(p)) bombDown = true;
            else if (ShowDetonate && DetonateRect.Contains(p)) detonateDown = true;
            else if (Held == Direction.None)
                foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                    if (DpadRect(d).Contains(p)) { Held = d; break; }
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                Consider(touch.position);
        }
        if (Input.touchCount == 0 && Input.GetMouseButton(0))
            Consider(Input.mousePosition);

        BombPressed = bombDown && !_bombWasDown;
        DetonatePressed = detonateDown && !_detonateWasDown;
        _bombWasDown = bombDown;
        _detonateWasDown = detonateDown;
    }

    private GUIStyle _style;

    private void OnGUI()
    {
        _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        var old = GUI.color;

        _style.fontSize = (int)(Unit * 0.45f);
        foreach (var (d, label) in new[] { (Direction.Up, "^"), (Direction.Down, "v"), (Direction.Left, "<"), (Direction.Right, ">") })
        {
            GUI.color = new Color(1f, 1f, 1f, Held == d ? 0.9f : 0.45f);
            GUI.Box(DpadRect(d), label, _style);
        }

        _style.fontSize = (int)(Unit * 0.3f);
        GUI.color = new Color(1f, 0.85f, 0.75f, _bombWasDown ? 0.95f : 0.6f);
        GUI.Box(BombRect, "BOMB", _style);
        if (ShowDetonate)
        {
            _style.fontSize = (int)(Unit * 0.2f);
            GUI.color = new Color(1f, 0.6f, 0.6f, _detonateWasDown ? 0.95f : 0.6f);
            GUI.Box(DetonateRect, "DETONATE", _style);
        }
        GUI.color = old;
    }
}
