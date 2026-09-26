using UnityEngine;

// The text boxes' sprites, drawn once in code: a rounded paper panel with an
// indigo edge, its soft shadow, a speech tail, and a white pill for the
// scrollbars and name tag. Drawn at Density texture pixels per canvas unit.
public static class PanelSprites
{
    public const float Density = 4f;
    public const float EdgeWidth = EdgePx / Density;
    public static readonly Vector2 TailBase = new Vector2((TailBaseLeft + TailBaseRight) / 2f / TailW, 1f);

    private const int EdgePx = 6;
    private const int RadiusPx = 40;
    private const int TailW = 144, TailH = 96, TailBaseLeft = 12, TailBaseRight = 96, TailTipX = 126;

    private static Sprite _panel, _shadow, _tail, _pill;

    public static Sprite Panel
    {
        get
        {
            if (_panel != null) return _panel;
            const int size = 96;
            var tex = NewTexture(size, size, "TextPanel");
            var pixels = new Color32[size * size];
            Vector2 half = new Vector2(size / 2f - 1f, size / 2f - 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                float d = RoundedBox(p, half, RadiusPx);
                float edge = Mathf.Clamp01(d + EdgePx + 0.5f);
                Color c = Color.Lerp(TextBoxTheme.Paper, TextBoxTheme.Edge, edge);
                c.a = Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = c;
            }
            return _panel = Finish(tex, pixels, RadiusPx + 4);
        }
    }

    public static Sprite Shadow
    {
        get
        {
            if (_shadow != null) return _shadow;
            const int size = 128, blur = 16;
            var tex = NewTexture(size, size, "TextPanelShadow");
            var pixels = new Color32[size * size];
            Vector2 half = new Vector2(size / 2f - blur, size / 2f - blur);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                float d = RoundedBox(p, half, RadiusPx);
                float a = 1f - Mathf.SmoothStep(0f, 1f, (d + blur) / (2f * blur));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return _shadow = Finish(tex, pixels, RadiusPx + blur + 4);
        }
    }

    // A tail pointing down and a little to the right. Its top edge has no
    // outline: it laps over the panel's edge line.
    public static Sprite Tail
    {
        get
        {
            if (_tail != null) return _tail;
            var tex = NewTexture(TailW, TailH, "TextPanelTail");
            var pixels = new Color32[TailW * TailH];
            Vector2 a = new Vector2(TailBaseLeft, TailH + 6f);
            Vector2 b = new Vector2(TailBaseRight, TailH + 6f);
            Vector2 tip = new Vector2(TailTipX, 2f);
            for (int y = 0; y < TailH; y++)
            for (int x = 0; x < TailW; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Triangle(p, a, b, tip);
                float toSides = Mathf.Min(Segment(p, a, tip), Segment(p, b, tip));
                float edge = d > 0f ? 1f : Mathf.Clamp01(EdgePx + 0.5f - toSides);
                Color c = Color.Lerp(TextBoxTheme.Paper, TextBoxTheme.Edge, edge);
                c.a = Mathf.Clamp01(0.5f - d);
                pixels[y * TailW + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return _tail = Sprite.Create(tex, new Rect(0, 0, TailW, TailH), TailBase, 100f, 0, SpriteMeshType.FullRect);
        }
    }

    public static Sprite Pill
    {
        get
        {
            if (_pill != null) return _pill;
            const int size = 32, radius = 8;
            var tex = NewTexture(size, size, "TextPanelPill");
            var pixels = new Color32[size * size];
            Vector2 half = new Vector2(size / 2f - 1f, size / 2f - 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - RoundedBox(p, half, radius)));
            }
            return _pill = Finish(tex, pixels, radius + 2);
        }
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
    }

    private static Sprite Finish(Texture2D tex, Color32[] pixels, int border)
    {
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    // Signed distance from p to a rounded box centered on the origin.
    private static float RoundedBox(Vector2 p, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    // Signed distance to a triangle (negative inside).
    private static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a)));
        return Inside(p, a, b, c) ? -d : d;
    }

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool neg = s1 < 0 || s2 < 0 || s3 < 0, pos = s1 > 0 || s2 > 0 || s3 > 0;
        return !(neg && pos);
    }

    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
}
