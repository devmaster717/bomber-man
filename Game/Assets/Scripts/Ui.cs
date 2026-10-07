using UnityEngine;

/// <summary>
/// The palace skin for every IMGUI screen, sized relative to the screen: navy panels and buttons framed in gold,
/// Cinzel Decorative for titles and Marcellus for text (both SIL Open Font License, in Resources/Palace/Fonts), and a
/// gold-latticed navy backdrop for menus. Textures are generated in code.
/// Call <see cref="Begin"/> at the start of each OnGUI so default controls (boxes, sliders) use the skin too.
/// Screens lay out on a canvas of <see cref="W"/> x <see cref="H"/> that sits inside the screen's safe area (clear of
/// notches and rounded corners); on screens squarer than 16:10, such as tablets, it is a centred 16:10 band so wide
/// layouts never get squeezed.
/// </summary>
public static class Ui
{
    public static readonly Color Cream = new Color(0.96f, 0.91f, 0.78f);
    public static readonly Color Gold = new Color(0.91f, 0.78f, 0.42f);
    private static readonly Color NavyTop = new Color(0.11f, 0.17f, 0.30f), NavyBottom = new Color(0.04f, 0.07f, 0.15f);

    private static GUISkin _skin;
    private static GUIStyle _button, _label, _title, _panel, _small, _smallButton, _field, _hud, _control;
    private static int _sizedFor;
    private static Font _body, _display;

    /// <summary>The layout canvas in screen (GUI) pixels: the safe area, cut to 16:10 if it is squarer.</summary>
    public static Rect Canvas
    {
        get
        {
            var s = Screen.safeArea;
            var r = new Rect(s.x, Screen.height - s.yMax, s.width, s.height); // safeArea's y runs up, GUI's down
            const float minAspect = 1.6f;
            if (r.width / r.height < minAspect)
            {
                float h = r.width / minAspect;
                r = new Rect(r.x, r.y + (r.height - h) / 2f, r.width, h);
            }
            return r;
        }
    }

    public static float W => Canvas.width;
    public static float H => Canvas.height;
    public static float U => H / 100f;

    /// <summary>
    /// Use the palace skin for this OnGUI pass and draw on the canvas (its origin is the canvas's corner). Pass false
    /// to draw in raw screen coordinates (the touch controls, which hit-test real touches).
    /// </summary>
    public static void Begin(bool onCanvas = true)
    {
        Ensure();
        GUI.skin = _skin;
        var c = Canvas;
        GUI.matrix = onCanvas ? Matrix4x4.Translate(new Vector3(c.x, c.y, 0f)) : Matrix4x4.identity;
    }

    // Runs a drawing step in raw screen coordinates, e.g. to cover the whole screen.
    private static void FullScreen(System.Action draw)
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        draw();
        GUI.matrix = matrix;
    }

    private static void Ensure()
    {
        if (_skin != null && _sizedFor == (int)H) return;
        _sizedFor = (int)H;
        _body ??= Resources.Load<Font>("Palace/Fonts/Marcellus-Regular");
        _display ??= Resources.Load<Font>("Palace/Fonts/CinzelDecorative-Bold");
        float u = U;

        _button = Framed(Textures.ButtonNormal, Textures.ButtonHover, Textures.ButtonActive, (int)(4.6f * u));
        _smallButton = Framed(Textures.ButtonNormal, Textures.ButtonHover, Textures.ButtonActive, (int)(3.4f * u));
        _label = new GUIStyle { font = _body, fontSize = (int)(4.4f * u), alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = false };
        _label.normal.textColor = Cream;
        _small = new GUIStyle(_label) { fontSize = (int)(3.4f * u) };
        _title = new GUIStyle(_label) { font = _display, fontSize = (int)(8.5f * u), wordWrap = false };
        _title.normal.textColor = Gold;
        _panel = new GUIStyle { border = new RectOffset(18, 18, 18, 18) };
        _panel.normal.background = Textures.Panel;
        _field = new GUIStyle(_label) { border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(12, 12, 4, 4), clipping = TextClipping.Clip };
        _field.normal.background = _field.hover.background = _field.focused.background = Textures.Field;
        _field.normal.textColor = _field.hover.textColor = _field.focused.textColor = Cream;
        _hud = new GUIStyle(_label) { fontSize = (int)(4f * u), alignment = TextAnchor.UpperLeft, wordWrap = false };
        _control = new GUIStyle(_label) { fontSize = (int)(3.6f * u), border = new RectOffset(32, 32, 32, 32) };
        _control.normal.background = Textures.Control;

        if (_skin == null) _skin = Object.Instantiate(GUI.skin);
        _skin.font = _body;
        _skin.box = new GUIStyle(_panel);
        _skin.button = _button;
        _skin.label = _label;
        _skin.textField = _field;
        _skin.horizontalSlider = new GUIStyle { border = new RectOffset(6, 6, 0, 0), fixedHeight = Mathf.Max(6f, 1.2f * u), margin = new RectOffset(0, 0, (int)(1.5f * u), 0) };
        _skin.horizontalSlider.normal.background = Textures.SliderTrack;
        _skin.horizontalSliderThumb = new GUIStyle { fixedWidth = 5f * u, fixedHeight = 5f * u };
        _skin.horizontalSliderThumb.normal.background = _skin.horizontalSliderThumb.hover.background =
            _skin.horizontalSliderThumb.active.background = Textures.SliderThumb;
        _skin.settings.cursorColor = Gold;
        _skin.settings.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
    }

    private static GUIStyle Framed(Texture2D normal, Texture2D hover, Texture2D active, int fontSize)
    {
        var s = new GUIStyle
        {
            font = _body,
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(16, 16, 16, 16),
            padding = new RectOffset(10, 10, 6, 6),
            wordWrap = true,
        };
        s.normal.background = normal;
        s.hover.background = hover;
        s.active.background = active;
        s.focused.background = normal;
        s.normal.textColor = s.focused.textColor = Cream;
        s.hover.textColor = new Color(1f, 0.95f, 0.8f);
        s.active.textColor = new Color(0.08f, 0.1f, 0.2f);
        return s;
    }

    public static GUIStyle Button { get { Ensure(); return _button; } }
    public static GUIStyle Label { get { Ensure(); return _label; } }
    public static GUIStyle Small { get { Ensure(); return _small; } }
    public static GUIStyle Title { get { Ensure(); return _title; } }
    public static GUIStyle SmallButton { get { Ensure(); return _smallButton; } }
    public static GUIStyle Field { get { Ensure(); return _field; } }
    public static GUIStyle Hud { get { Ensure(); return _hud; } }

    /// <summary>The round, gold-ringed style of the on-screen controls.</summary>
    public static GUIStyle Control { get { Ensure(); return _control; } }

    public static Texture2D Arrow => Textures.Arrow;
    public static Texture2D AvatarRing => Textures.Ring;

    /// <summary>The menu backdrop: deep navy lit from the centre, a faint gold lattice, and a double gold frame.</summary>
    public static void Backdrop()
    {
        Ensure();
        FullScreen(() =>
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(screen, Textures.Radial, ScaleMode.StretchToFill);
            float tile = 9f * U;
            GUI.DrawTextureWithTexCoords(screen, Textures.Lattice, new Rect(0, 0, Screen.width / tile, Screen.height / tile));
        });
        // The gold frame follows the canvas, so it stays clear of notches.
        float m = 1.6f * U;
        Frame(new Rect(m, m, W - 2 * m, H - 2 * m), Mathf.Max(2f, 0.3f * U), Gold);
        Frame(new Rect(m * 1.6f, m * 1.6f, W - 3.2f * m, H - 3.2f * m), 1f, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
    }

    /// <summary>Covers the whole screen in navy at the given opacity (screen transitions).</summary>
    public static void Fade(float alpha)
    {
        var old = GUI.color;
        GUI.color = new Color(NavyBottom.r, NavyBottom.g, NavyBottom.b, Mathf.Clamp01(alpha));
        FullScreen(() => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture));
        GUI.color = old;
    }

    /// <summary>A navy band across the top of the arena behind the HUD text, edged in gold.</summary>
    public static void HudBar(float heightFraction)
    {
        Ensure();
        float h = Canvas.y + H * heightFraction;
        FullScreen(() => GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Textures.HudFade, ScaleMode.StretchToFill));
    }

    private static void Frame(Rect r, float thickness, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    /// <summary>A dimmed full-screen backdrop with a centred panel; returns the panel's rect.</summary>
    public static Rect Panel(float widthFraction, float heightFraction)
    {
        Ensure();
        var old = GUI.color;
        GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.65f);
        FullScreen(() => GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture));
        GUI.color = old;
        float w = W * widthFraction, h = H * heightFraction;
        var r = new Rect((W - w) / 2, (H - h) / 2, w, h);
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

    /// <summary>The skin's textures, drawn once with soft (anti-aliased) edges.</summary>
    private static class Textures
    {
        private static readonly Color Clear = new Color(0, 0, 0, 0);

        public static readonly Texture2D ButtonNormal = RoundedFrame(64, 12, NavyTop, NavyBottom, Gold, 2.2f, 0.95f);
        public static readonly Texture2D ButtonHover = RoundedFrame(64, 12, new Color(0.16f, 0.24f, 0.42f), new Color(0.07f, 0.12f, 0.24f), new Color(1f, 0.88f, 0.55f), 2.6f, 0.97f);
        public static readonly Texture2D ButtonActive = RoundedFrame(64, 12, new Color(0.98f, 0.86f, 0.52f), new Color(0.72f, 0.53f, 0.2f), new Color(1f, 0.95f, 0.75f), 2.2f, 1f);
        public static readonly Texture2D Panel = Panelled();
        public static readonly Texture2D Field = RoundedFrame(48, 8, new Color(0.02f, 0.03f, 0.07f), new Color(0.04f, 0.06f, 0.12f), new Color(Gold.r, Gold.g, Gold.b, 0.8f), 1.4f, 1f);
        public static readonly Texture2D SliderTrack = RoundedFrame(24, 4, new Color(0.45f, 0.36f, 0.16f), new Color(0.3f, 0.22f, 0.09f), Gold, 1f, 1f, 12);
        public static readonly Texture2D SliderThumb = Disc(48, new Color(1f, 0.9f, 0.6f), new Color(0.7f, 0.5f, 0.18f), new Color(1f, 0.96f, 0.8f), 2f, 1f);
        public static readonly Texture2D Control = Disc(96, new Color(0.1f, 0.15f, 0.28f), new Color(0.03f, 0.05f, 0.12f), Gold, 3.5f, 0.55f);
        public static readonly Texture2D Ring = Disc(96, Clear, Clear, Gold, 4f, 1f);
        public static readonly Texture2D Arrow = Triangle(48);
        public static readonly Texture2D Radial = RadialGradient(128);
        public static readonly Texture2D Lattice = LatticeTile(64);
        public static readonly Texture2D HudFade = Fade(4, 64);

        private static Texture2D New(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };

        // Signed distance from a point to a rounded rectangle centred in a size x size square (negative inside).
        private static float RoundedDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2f) - (w / 2f - r), qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
            return Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        /// <summary>A rounded rectangle: vertical gradient fill, a border, a faint highlight along the top.</summary>
        private static Texture2D RoundedFrame(int size, float radius, Color top, Color bottom, Color border, float borderWidth, float alpha, int height = 0)
        {
            int w = size, h = height > 0 ? height : size;
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, w, h, Mathf.Min(radius, h / 2f));
                float inside = Mathf.Clamp01(0.5f - d);
                float edge = Mathf.Clamp01(borderWidth + 0.5f + d) * inside;
                var fill = Color.Lerp(bottom, top, (y + 0.5f) / h);
                // A soft sheen just inside the top border.
                fill += new Color(1f, 0.95f, 0.8f) * 0.08f * Mathf.Clamp01(1f - Mathf.Abs(h - 1 - y - borderWidth - 2f) / 2f);
                var c = Color.Lerp(fill, border, edge);
                c.a = inside * Mathf.Lerp(alpha * fill.a, border.a, edge);
                px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>A panel: translucent navy with a gold border and a thin inner gold line.</summary>
        private static Texture2D Panelled()
        {
            const int s = 64;
            var t = New(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, s, s, 10f);
                float inside = Mathf.Clamp01(0.5f - d);
                float outer = Mathf.Clamp01(2.5f + d);
                float inner = Mathf.Clamp01(1f - Mathf.Abs(d + 6f));
                var c = Color.Lerp(NavyBottom, NavyTop, (y + 0.5f) / s * 0.6f);
                c = Color.Lerp(c, Gold, Mathf.Max(outer, inner * 0.7f));
                c.a = inside * Mathf.Lerp(0.94f, 1f, outer);
                px[y * s + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>A disc with a radial gradient fill and a ring.</summary>
        private static Texture2D Disc(int s, Color centre, Color rim, Color ring, float ringWidth, float alpha)
        {
            var t = New(s, s);
            var px = new Color[s * s];
            float r = s / 2f - 1f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f, dist = Mathf.Sqrt(dx * dx + dy * dy);
                float inside = Mathf.Clamp01(r - dist + 0.5f);
                float onRing = Mathf.Clamp01(ringWidth + 0.5f - (r - dist)) * inside;
                var fill = Color.Lerp(centre, rim, dist / r);
                var c = Color.Lerp(fill, ring, onRing);
                c.a = inside * Mathf.Lerp(alpha * fill.a, ring.a, onRing);
                px[y * s + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>A soft white triangle pointing up, for the D-pad.</summary>
        private static Texture2D Triangle(int s)
        {
            var t = New(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s; // v up
                float halfWidth = (0.8f - v) * 0.55f;
                float a = v > 0.2f && v < 0.8f ? Mathf.Clamp01((halfWidth - Mathf.Abs(u - 0.5f)) * s) * Mathf.Clamp01((v - 0.2f) * s) : 0f;
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D RadialGradient(int s)
        {
            var t = New(s, s);
            var px = new Color[s * s];
            var centre = new Color(0.10f, 0.16f, 0.30f);
            var edge = new Color(0.015f, 0.025f, 0.06f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.55f;
                px[y * s + x] = Color.Lerp(centre, edge, Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dy * dy) * 1.6f));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>A repeating diamond lattice with small dots at the crossings, in faint gold.</summary>
        private static Texture2D LatticeTile(int s)
        {
            var t = New(s, s, TextureWrapMode.Repeat);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                float line = Mathf.Min(Mathf.Abs(Mathf.Repeat(u + v, 1f) - 0.5f), Mathf.Abs(Mathf.Repeat(u - v, 1f) - 0.5f)) * s;
                float a = Mathf.Clamp01(1.2f - line) * 0.07f;
                float dot = Mathf.Min(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0f)), Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 1f)));
                a = Mathf.Max(a, Mathf.Clamp01(2.5f - dot * s) * 0.12f);
                px[y * s + x] = new Color(Gold.r, Gold.g, Gold.b, a);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Navy fading out downwards, with a gold line along the bottom of the solid part.</summary>
        private static Texture2D Fade(int w, int h)
        {
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float fromTop = 1f - (y + 0.5f) / h;
                var c = new Color(NavyBottom.r, NavyBottom.g, NavyBottom.b, Mathf.Lerp(0.85f, 0f, Mathf.SmoothStep(0.55f, 1f, fromTop)));
                if (Mathf.Abs(fromTop - 0.58f) < 0.012f) c = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
