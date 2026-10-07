using BombArena.Core;
using UnityEngine;

/// <summary>
/// On-screen controls: a D-pad or a virtual joystick for movement, a large Bomb button and a smaller Detonate
/// button above it (shown only while enabled). Movement sits bottom-left and the buttons bottom-right, swapped
/// in left-handed mode. Reads every touch so movement and buttons work together, plus the mouse in the editor.
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

    public bool UseJoystick { get; set; }
    public bool LeftHanded { get; set; }

    /// <summary>Button size as a fraction of the default (0.75–1.5).</summary>
    public float Scale { get; set; } = 1f;

    private float Unit => Screen.height * 0.14f * Scale;
    private float Margin => Screen.height * 0.05f;

    private bool _bombWasDown, _detonateWasDown;
    private int _stickFinger = -2;      // -1 is the mouse; -2 means no finger on the stick
    private Vector2 _stickOrigin, _stickNow;

    // Movement controls live on this side; buttons on the other.
    private bool MoveOnLeft => !LeftHanded;

    private float SideX(float xFromEdge, float width, bool left) => left ? xFromEdge : Screen.width - xFromEdge - width;

    // Rects in GUI space (origin top-left).
    private Rect DpadRect(Direction d)
    {
        float s = Unit, m = Margin;
        float cx = SideX(m + s, s, MoveOnLeft), cy = Screen.height - m - 2 * s;
        return d switch
        {
            Direction.Up => new Rect(cx, cy - s, s, s),
            Direction.Down => new Rect(cx, cy + s, s, s),
            Direction.Left => new Rect(cx - s, cy, s, s),
            Direction.Right => new Rect(cx + s, cy, s, s),
            _ => Rect.zero,
        };
    }

    /// <summary>The joystick accepts touches that start anywhere in the lower movement-side area.</summary>
    private Rect StickZone => new Rect(MoveOnLeft ? 0 : Screen.width * 0.6f, Screen.height * 0.3f, Screen.width * 0.4f, Screen.height * 0.7f);

    private Rect BombRect
    {
        get
        {
            float size = Unit * 1.6f;
            return new Rect(SideX(Margin, size, !MoveOnLeft), Screen.height - Margin - size, size, size);
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

    private static Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

    private void Update()
    {
        Held = Direction.None;
        bool bombDown = false, detonateDown = false, stickSeen = false;

        void Consider(int finger, Vector2 screenPos, bool began)
        {
            var p = ToGui(screenPos);
            if (BombRect.Contains(p)) { bombDown = true; return; }
            if (ShowDetonate && DetonateRect.Contains(p)) { detonateDown = true; return; }

            if (UseJoystick)
            {
                if (_stickFinger == -2 && began && StickZone.Contains(p)) { _stickFinger = finger; _stickOrigin = p; }
                if (finger == _stickFinger) { _stickNow = p; stickSeen = true; Held = StickDirection(); }
            }
            else if (Held == Direction.None)
            {
                foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                    if (DpadRect(d).Contains(p)) { Held = d; break; }
            }
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                Consider(touch.fingerId, touch.position, touch.phase == TouchPhase.Began);
        }
        if (Input.touchCount == 0 && Input.GetMouseButton(0))
            Consider(-1, Input.mousePosition, Input.GetMouseButtonDown(0));

        if (!stickSeen) _stickFinger = -2;
        BombPressed = bombDown && !_bombWasDown;
        DetonatePressed = detonateDown && !_detonateWasDown;
        _bombWasDown = bombDown;
        _detonateWasDown = detonateDown;
    }

    /// <summary>Four directions only: the larger axis of the drag wins, past a small dead zone.</summary>
    private Direction StickDirection()
    {
        var d = _stickNow - _stickOrigin;
        if (d.magnitude < Unit * 0.2f) return Direction.None;
        if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0 ? Direction.Right : Direction.Left;
        return d.y > 0 ? Direction.Down : Direction.Up; // GUI y grows downwards
    }

    private GUIStyle _style;

    private void OnGUI()
    {
        Ui.Begin();
        // Round, gold-ringed buttons from the palace skin; a copy, since font sizes change here.
        _style ??= new GUIStyle(Ui.Control);
        var old = GUI.color;

        if (UseJoystick)
        {
            float r = Unit * 1.1f;
            var centre = _stickFinger != -2 ? _stickOrigin
                : new Vector2(SideX(Margin + r, 0, MoveOnLeft), Screen.height - Margin - r);
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            GUI.Box(new Rect(centre.x - r, centre.y - r, 2 * r, 2 * r), GUIContent.none, _style);
            var knob = _stickFinger != -2 ? Vector2.ClampMagnitude(_stickNow - _stickOrigin, r * 0.6f) + _stickOrigin : centre;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Box(new Rect(knob.x - r * 0.4f, knob.y - r * 0.4f, r * 0.8f, r * 0.8f), GUIContent.none, _style);
        }
        else
        {
            _style.fontSize = (int)(Unit * 0.45f);
            foreach (var (d, angle) in new[] { (Direction.Up, 0f), (Direction.Right, 90f), (Direction.Down, 180f), (Direction.Left, 270f) })
            {
                var r = DpadRect(d);
                GUI.color = new Color(1f, 1f, 1f, Held == d ? 0.95f : 0.55f);
                GUI.Box(r, GUIContent.none, _style);
                // A gold arrow, turned to point its way.
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, r.center);
                GUI.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, Held == d ? 1f : 0.8f);
                GUI.DrawTexture(new Rect(r.center.x - r.width * 0.22f, r.center.y - r.height * 0.22f, r.width * 0.44f, r.height * 0.44f), Ui.Arrow);
                GUI.matrix = matrix;
            }
        }

        _style.fontSize = (int)(Unit * 0.3f);
        GUI.color = new Color(1f, 0.85f, 0.75f, _bombWasDown ? 0.95f : 0.6f);
        GUI.Box(BombRect, Text.BombButton, _style);
        if (ShowDetonate)
        {
            _style.fontSize = (int)(Unit * 0.2f);
            GUI.color = new Color(1f, 0.6f, 0.6f, _detonateWasDown ? 0.95f : 0.6f);
            GUI.Box(DetonateRect, Text.DetonateButton, _style);
        }
        GUI.color = old;
    }
}
