using BombArena.Core;
using UnityEngine;

/// <summary>
/// The animated part of the "Stage clear!" panel: the earned stars pop into their slots one by one, each with a
/// chime a step higher, then the jewels count up beside an emerald, then the breakdown of the reward fades in.
/// A tap anywhere skips to the end. Drawing only: the reward was paid before the panel opened.
/// </summary>
public sealed class ClearCelebration
{
    // Seconds from the panel opening.
    private const float FirstStar = 0.45f, StarGap = 0.42f, StarPop = 0.35f, Flash = 0.45f;
    private const float AfterStars = 0.3f, CountTime = 1.1f, TickGap = 0.07f, BreakdownFade = 0.4f;

    private readonly int _stars;
    private readonly ClearReward? _reward;
    private readonly float _start;
    private bool _skipped;
    private int _starsChimed;
    private bool _countStarted, _countDone;
    private long _lastShown;
    private float _lastTick;
    private GUIStyle _bigNumbers;

    public ClearCelebration(int stars, ClearReward? reward)
    {
        _stars = Mathf.Clamp(stars, 0, 3);
        _reward = reward;
        _start = Time.unscaledTime;
    }

    private float CountStart => FirstStar + StarGap * Mathf.Max(0, _stars - 1) + StarPop + AfterStars;
    private float Elapsed => _skipped ? 1000f : Time.unscaledTime - _start;

    /// <summary>The three star slots, centred in <paramref name="row"/>.</summary>
    public void DrawStars(Rect row)
    {
        SkipOnTap();
        float t = Elapsed, size = row.height, gap = size * 0.25f;
        float x = row.center.x - (3 * size + 2 * gap) / 2f;
        for (int i = 0; i < 3; i++)
        {
            var slot = new Rect(x + i * (size + gap), row.y, size, size);
            GUI.DrawTexture(slot, Ui.Star(false), ScaleMode.ScaleToFit);
            if (i >= _stars) continue;

            float at = FirstStar + i * StarGap;
            if (t < at) continue;
            if (i >= _starsChimed && !_skipped)
            {
                _starsChimed = i + 1;
                if (Feedback.Instance != null) Feedback.Instance.StarAwarded(i);
            }
            float p = Mathf.Clamp01((t - at) / StarPop);
            // A gold flash that swells and fades behind the star as it lands.
            float f = Mathf.Clamp01((t - at) / Flash);
            if (f < 1f) Draw(slot, Ui.Star(true), 1.2f + 0.9f * f, 0f, 0.55f * (1f - f));
            Draw(slot, Ui.Star(true), OverShoot(p), -30f * (1f - p), 1f);
        }
    }

    /// <summary>The jewels earned counting up beside an emerald, then the breakdown fading in below.</summary>
    public void DrawJewels(Rect row, Rect breakdown)
    {
        if (_reward is not ClearReward r) return;
        SkipOnTap();
        float t = Elapsed - CountStart;
        if (t < 0f) return;
        if (!_countStarted) { _countStarted = true; _lastTick = t; }

        float p = Mathf.Clamp01(t / CountTime);
        long shown = (long)Mathf.Round(r.Total * (1f - Mathf.Pow(1f - p, 3f))); // fast at first, easing into the total
        if (!_skipped && shown != _lastShown && t - _lastTick >= TickGap && p < 1f)
        {
            _lastTick = t;
            if (Feedback.Instance != null) Feedback.Instance.JewelTick();
        }
        _lastShown = shown;
        if (p >= 1f && !_countDone)
        {
            _countDone = true;
            if (Feedback.Instance != null && !_skipped) Feedback.Instance.JewelsAwarded();
        }

        _bigNumbers ??= new GUIStyle(Ui.Numbers) { fontSize = (int)(Ui.Numbers.fontSize * 1.5f), alignment = TextAnchor.MiddleLeft };
        // A little bounce when the count lands on the total.
        float bounce = p >= 1f ? 1f + 0.18f * Mathf.Sin(Mathf.Clamp01((t - CountTime) / 0.3f) * Mathf.PI) : 1f;
        string text = "+" + shown;
        float icon = row.height, textW = _bigNumbers.CalcSize(new GUIContent(text)).x, gap = icon * 0.3f;
        float x = row.center.x - (icon + gap + textW) / 2f;
        var matrix = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * bounce, row.center + new Vector2(GUI.matrix.m03, GUI.matrix.m13));
        GUI.DrawTexture(new Rect(x, row.y, icon, icon), Ui.Jewel, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(x + icon + gap, row.y, textW + 4f, row.height), text, _bigNumbers);
        GUI.matrix = matrix;

        float fade = Mathf.Clamp01((t - CountTime) / BreakdownFade);
        if (fade <= 0f) return;
        var old = GUI.color;
        GUI.color = new Color(old.r, old.g, old.b, old.a * fade);
        GUI.Label(breakdown, Text.JewelBreakdown(r.ClearJewels, r.StarBonus, r.FirstThreeStars), Ui.SmallNumbers);
        GUI.color = old;
    }

    // A tap that no button took finishes the animation at once.
    private void SkipOnTap()
    {
        if (!_skipped && Event.current.type == EventType.MouseDown) _skipped = true;
    }

    // Draws a texture scaled and turned about the rect's centre, at the given opacity.
    private static void Draw(Rect r, Texture2D texture, float scale, float degrees, float alpha)
    {
        if (scale <= 0f || alpha <= 0f) return;
        var matrix = GUI.matrix;
        var old = GUI.color;
        var pivot = r.center + new Vector2(GUI.matrix.m03, GUI.matrix.m13);
        GUIUtility.RotateAroundPivot(degrees, pivot);
        GUIUtility.ScaleAroundPivot(Vector2.one * scale, pivot);
        GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
        GUI.DrawTexture(r, texture, ScaleMode.ScaleToFit);
        GUI.color = old;
        GUI.matrix = matrix;
    }

    // 0 to 1 with an overshoot past 1 before settling ("ease out back").
    private static float OverShoot(float p)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float q = p - 1f;
        return p <= 0f ? 0f : 1f + c3 * q * q * q + c1 * q * q;
    }
}
