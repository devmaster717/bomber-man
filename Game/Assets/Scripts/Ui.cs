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

    /// <summary>A cut ruby: the jewels icon.</summary>
    public static Texture2D Ruby => Textures.Ruby;

    /// <summary>A glossy red heart: the lives icon.</summary>
    public static Texture2D Heart => Textures.Heart;

    /// <summary>A gold star for a star earned, or an empty star slot for one not yet earned.</summary>
    public static Texture2D Star(bool earned) => earned ? Textures.Star : Textures.StarEmpty;

    /// <summary>
    /// Draws icon-and-text pairs side by side, centred in <paramref name="row"/> (icons a little taller than the
    /// text, with a wider gap between pairs).
    /// </summary>
    public static void IconRow(Rect row, GUIStyle style, params (Texture2D icon, string text)[] items)
    {
        Ensure();
        var left = new GUIStyle(style) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
        float icon = style.fontSize * 1.35f, gap = style.fontSize * 0.35f, between = style.fontSize * 1.6f;
        float total = -between;
        foreach (var (_, text) in items) total += icon + gap + left.CalcSize(new GUIContent(text)).x + between;
        float x = row.x + (row.width - total) / 2f;
        foreach (var (tex, text) in items)
        {
            GUI.DrawTexture(new Rect(x, row.y + (row.height - icon) / 2f, icon, icon), tex, ScaleMode.ScaleToFit);
            x += icon + gap;
            float w = left.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(x, row.y, w + 2f, row.height), text, left);
            x += w + between;
        }
    }

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
        // Painted on first use, not with the fields above: static fields initialise in source order, and the shapes
        // these read are declared further down.
        private static Texture2D _ruby, _heart, _star, _starEmpty;
        public static Texture2D Ruby => _ruby != null ? _ruby : _ruby = Paint(96, RubyAt);
        public static Texture2D Heart => _heart != null ? _heart : _heart = Paint(96, HeartAt);
        public static Texture2D Star => _star != null ? _star : _star = Paint(96, p => StarAt(p, true));
        public static Texture2D StarEmpty => _starEmpty != null ? _starEmpty : _starEmpty = Paint(96, p => StarAt(p, false));

        /// <summary>
        /// An icon drawn from a function of the point (0..1 across, 0..1 down) that returns its colour, sampled 4 x 4
        /// times per pixel so edges come out smooth.
        /// </summary>
        private static Texture2D Paint(int s, System.Func<Vector2, Color> at)
        {
            const int n = 4;
            var t = New(s, s);
            t.filterMode = FilterMode.Trilinear;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var c = at(new Vector2((x + (i + 0.5f) / n) / s, 1f - (y + (j + 0.5f) / n) / s));
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                }
                px[y * s + x] = a > 0 ? new Color(r / a, g / a, b / a, a / (n * n)) : Clear;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static bool Inside(Vector2 p, params Vector2[] poly)
        {
            bool? sign = null;
            for (int i = 0; i < poly.Length; i++)
            {
                float c = Cross(poly[i], poly[(i + 1) % poly.Length], p);
                if (Mathf.Abs(c) < 1e-7f) continue;
                if (sign == null) sign = c > 0;
                else if (sign != c > 0) return false;
            }
            return true;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ---- the ruby (jewels): a cut gem seen from the side, crown on top and pavilion below ----

        private static readonly Vector2 GemA = new Vector2(0.08f, 0.38f), GemB = new Vector2(0.3f, 0.15f), GemC = new Vector2(0.7f, 0.15f),
            GemD = new Vector2(0.92f, 0.38f), GemE = new Vector2(0.5f, 0.9f), GemP = new Vector2(0.35f, 0.38f), GemQ = new Vector2(0.65f, 0.38f);

        private static readonly (Vector2[] facet, Color colour)[] GemFacets =
        {
            (new[] { GemB, GemC, GemQ, GemP }, new Color(1f, 0.5f, 0.56f)),    // table, catching the light
            (new[] { GemA, GemB, GemP }, new Color(0.96f, 0.26f, 0.36f)),      // crown, left
            (new[] { GemC, GemD, GemQ }, new Color(0.84f, 0.1f, 0.2f)),        // crown, right
            (new[] { GemA, GemP, GemE }, new Color(0.78f, 0.06f, 0.17f)),      // pavilion, left
            (new[] { GemP, GemQ, GemE }, new Color(0.9f, 0.14f, 0.24f)),       // pavilion, centre
            (new[] { GemQ, GemD, GemE }, new Color(0.5f, 0.02f, 0.1f)),        // pavilion, right, in shadow
        };

        private static readonly Vector2[] GemOutline = { GemA, GemB, GemC, GemD, GemE };

        private static Color RubyAt(Vector2 p)
        {
            if (!Inside(p, GemOutline)) return Clear;
            var c = GemFacets[0].colour;
            foreach (var (facet, colour) in GemFacets)
                if (Inside(p, facet)) { c = colour; break; }
            // Bright hairlines along the facet edges, a dark rim along the outline.
            float inner = float.MaxValue, outer = float.MaxValue;
            foreach (var (facet, _) in GemFacets)
                for (int i = 0; i < facet.Length; i++) inner = Mathf.Min(inner, SegmentDistance(p, facet[i], facet[(i + 1) % facet.Length]));
            for (int i = 0; i < GemOutline.Length; i++) outer = Mathf.Min(outer, SegmentDistance(p, GemOutline[i], GemOutline[(i + 1) % GemOutline.Length]));
            c = Color.Lerp(c, new Color(1f, 0.75f, 0.78f), 0.55f * Mathf.Clamp01(1f - inner / 0.012f));
            c = Color.Lerp(c, new Color(0.3f, 0f, 0.05f), Mathf.Clamp01(1f - (outer - 0.012f) / 0.012f));
            // A four-pointed sparkle on the table.
            var d = p - new Vector2(0.38f, 0.25f);
            float sparkle = Mathf.Clamp01(1f - (Mathf.Abs(d.x) * Mathf.Abs(d.y) * 900f + d.magnitude * 9f));
            return Color.Lerp(c, Color.white, sparkle);
        }

        // ---- the heart (lives): glossy red, lit from the top left ----

        // Signed distance to a heart whose tip is at the origin and which rises to y = 1.1 (two round lobes over a
        // square turned 45 degrees); negative inside.
        private static float HeartDistance(float x, float y)
        {
            x = Mathf.Abs(x);
            if (y + x > 1f) return new Vector2(x - 0.25f, y - 0.75f).magnitude - Mathf.Sqrt(2f) / 4f;
            float m = 0.5f * Mathf.Max(x + y, 0f);
            float d = Mathf.Min(new Vector2(x, y - 1f).sqrMagnitude, new Vector2(x - m, y - m).sqrMagnitude);
            return Mathf.Sqrt(d) * Mathf.Sign(x - y);
        }

        private static Color HeartAt(Vector2 p)
        {
            float x = (p.x - 0.5f) * 1.3f, y = (0.94f - p.y) * 1.3f;
            float edge = -HeartDistance(x, y);
            if (edge < 0f) return Clear;
            // Lit from the top left, a deeper red towards the rim, and a dark outline.
            var c = Color.Lerp(new Color(1f, 0.38f, 0.44f), new Color(0.66f, 0.03f, 0.11f), Mathf.Clamp01((x - (y - 0.55f)) * 0.9f + 0.5f));
            c = Color.Lerp(c, new Color(0.5f, 0.01f, 0.08f), Mathf.Clamp01(1f - edge / 0.09f) * 0.45f);
            c = Color.Lerp(c, new Color(0.32f, 0f, 0.05f), Mathf.Clamp01(1f - (edge - 0.012f) / 0.012f));
            // A soft white gloss on the left lobe.
            float gloss = new Vector2((x + 0.27f) / 0.13f, (y - 0.82f) / 0.075f).magnitude;
            return Color.Lerp(c, Color.white, 0.8f * Mathf.Clamp01(1f - gloss));
        }

        // ---- the star (stage stars): a bevelled gold star, or an empty slot for one not yet earned ----

        private static Color StarAt(Vector2 p, bool earned)
        {
            var centre = new Vector2(0.5f, 0.54f);
            const float outerR = 0.47f, innerR = 0.2f;
            var points = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? outerR : innerR;
                points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
            int facet = -1;
            for (int i = 0; i < 10; i++)
                if (Inside(p, centre, points[i], points[(i + 1) % 10])) { facet = i; break; }
            if (facet < 0) return Clear;
            float outline = float.MaxValue;
            for (int i = 0; i < 10; i++) outline = Mathf.Min(outline, SegmentDistance(p, points[i], points[(i + 1) % 10]));
            if (!earned)
            {
                var slot = new Color(0.06f, 0.09f, 0.18f, 0.9f);
                return Color.Lerp(slot, new Color(Gold.r, Gold.g, Gold.b, 0.75f), Mathf.Clamp01(1f - (outline - 0.02f) / 0.012f));
            }
            // Each arm is two facets: the one facing the light is pale gold, the other deeper; the upper arms brighter.
            float light = Vector2.Dot(((points[facet] + points[(facet + 1) % 10]) / 2f - centre).normalized, new Vector2(-0.6f, -0.8f));
            var c = Color.Lerp(new Color(0.78f, 0.5f, 0.1f), new Color(1f, 0.92f, 0.58f), Mathf.Clamp01(light * 0.6f + 0.5f));
            c = Color.Lerp(c, new Color(0.42f, 0.26f, 0.04f), Mathf.Clamp01(1f - (outline - 0.012f) / 0.012f));
            return c;
        }

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
