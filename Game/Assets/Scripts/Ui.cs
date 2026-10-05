using UnityEngine;

/// <summary>Shared IMGUI styles and layout helpers for the placeholder screens, sized relative to the screen.</summary>
public static class Ui
{
    private static GUIStyle _button, _label, _title, _panel, _small, _smallButton;
    private static int _sizedFor;

    public static float U => Screen.height / 100f;

    private static void Ensure()
    {
        if (_button != null && _sizedFor == Screen.height) return;
        _sizedFor = Screen.height;
        _button = new GUIStyle(GUI.skin.button) { fontSize = (int)(5 * U), fontStyle = FontStyle.Bold };
        _label = new GUIStyle(GUI.skin.label) { fontSize = (int)(4.5f * U), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        _small = new GUIStyle(_label) { fontSize = (int)(3.5f * U) };
        _title = new GUIStyle(_label) { fontSize = (int)(9 * U), fontStyle = FontStyle.Bold };
        _panel = new GUIStyle(GUI.skin.box);
        _smallButton = new GUIStyle(GUI.skin.button) { fontSize = (int)(3.5f * U) };
    }

    public static GUIStyle Button { get { Ensure(); return _button; } }
    public static GUIStyle Label { get { Ensure(); return _label; } }
    public static GUIStyle Small { get { Ensure(); return _small; } }
    public static GUIStyle Title { get { Ensure(); return _title; } }
    public static GUIStyle SmallButton { get { Ensure(); return _smallButton; } }

    /// <summary>A dimmed full-screen backdrop with a centred panel; returns the panel's rect.</summary>
    public static Rect Panel(float widthFraction, float heightFraction)
    {
        Ensure();
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
        float w = Screen.width * widthFraction, h = Screen.height * heightFraction;
        var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        GUI.Box(r, GUIContent.none, _panel);
        GUI.Box(r, GUIContent.none, _panel);
        return r;
    }

    /// <summary>Vertical layout cursor inside a rect.</summary>
    public sealed class Column
    {
        private readonly Rect _area;
        private float _y;

        public Column(Rect area, float topPadding = 3f)
        {
            _area = area;
            _y = area.y + topPadding * U;
        }

        public Rect Next(float heightU, float gapU = 2f)
        {
            var r = new Rect(_area.x + 4 * U, _y, _area.width - 8 * U, heightU * U);
            _y += (heightU + gapU) * U;
            return r;
        }
    }

    public static string Clock(long seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
