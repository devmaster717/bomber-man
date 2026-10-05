using BombArena.Core;
using UnityEngine;

/// <summary>
/// On-screen D-pad in the bottom-left corner. Reads every touch (so it keeps working when a bomb button
/// is added later) and the mouse in the editor.
/// </summary>
public sealed class TouchDpad : MonoBehaviour
{
    public Direction Held { get; private set; }

    private float ButtonSize => Screen.height * 0.14f;
    private float Margin => Screen.height * 0.05f;

    // Button rects in GUI space (origin top-left).
    private Rect RectFor(Direction d)
    {
        float s = ButtonSize, m = Margin;
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

    private void Update()
    {
        Held = Direction.None;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
            var hit = HitTest(touch.position);
            if (hit != Direction.None) { Held = hit; return; }
        }
        if (Input.touchCount == 0 && Input.GetMouseButton(0))
            Held = HitTest(Input.mousePosition);
    }

    private Direction HitTest(Vector2 screenPos)
    {
        var guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
        foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            if (RectFor(d).Contains(guiPos)) return d;
        return Direction.None;
    }

    private GUIStyle _style;

    private void OnGUI()
    {
        _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
        _style.fontSize = (int)(ButtonSize * 0.45f);
        var old = GUI.color;
        foreach (var (d, label) in new[] { (Direction.Up, "^"), (Direction.Down, "v"), (Direction.Left, "<"), (Direction.Right, ">") })
        {
            GUI.color = Held == d ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
            GUI.Box(RectFor(d), label, _style);
        }
        GUI.color = old;
    }
}
