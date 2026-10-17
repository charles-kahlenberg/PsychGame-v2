using UnityEngine;

// The text boxes' sprites: the frame (art from Resources/TextBox), and, drawn
// once in code, its soft shadow, a speech tail in the frame's pixel style,
// and a white pill for the scrollbars and name tag. Also the cards' hover
// glow. All at Density texture pixels per canvas unit.
public static class PanelSprites
{
    public const float Density = 4f;
    public const float EdgeWidth = EdgePx / Density;
    public const float GlowSpread = GlowPx / Density;
    public static readonly Vector2 TailBase = new Vector2((TailBaseLeft + TailBaseRight) / 2f / TailW, 1f);

    private const int EdgePx = 4; // the frame's outline: one art pixel
    private const int RadiusPx = 40; // the shadow's corners, about the frame's
    private const int TailW = 144, TailH = 96, TailBaseLeft = 12, TailBaseRight = 96, TailTipX = 126;
    private const int GlowPx = 48, GlowRimPx = 12, GlowRadiusPx = 8; // the card back's corners are only slightly rounded

    private static Sprite _panel, _shadow, _tail, _pill, _glow;

    // The text box frame (Resources/TextBox/Frame): the hand-drawn corner and
    // edge, mirrored into a 9-slice at 4 texels per art pixel (like the
    // PixelButtons), point-filtered so the corners stay crisp at any size.
    public static Sprite Panel => _panel != null ? _panel : _panel = Resources.Load<Sprite>("TextBox/Frame");

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

    // A tail pointing down and a little to the right, in the frame's colours
    // and one-art-pixel outline. Its top edge has no outline: it laps over
    // the frame's edge line.
    public static Sprite Tail => _tail != null ? _tail : _tail = MakeTail(TailTipX, "TextPanelTail");

    // The same, pointing straight down (the grading screen's score, right above Brainy).
    public static Sprite StraightTail => _straightTail != null ? _straightTail : _straightTail = MakeTail((TailBaseLeft + TailBaseRight) / 2, "TextPanelStraightTail");

    private static Sprite _straightTail;

    // The frame art's colours (Resources/TextBox/Frame).
    private static readonly Color32 ArtLine = new Color32(8, 6, 6, 255), ArtFill = new Color32(255, 255, 255, 255);

    // Drawn an art pixel (EdgePx texels) at a time: inside the triangle is
    // fill, and outline where a neighbour is outside, so the sides step like
    // the frame's corners.
    private static Sprite MakeTail(int tipX, string name)
    {
        var tex = NewTexture(TailW, TailH, name);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color32[TailW * TailH];
        Vector2 a = new Vector2(TailBaseLeft, TailH + 6f) / EdgePx;
        Vector2 b = new Vector2(TailBaseRight, TailH + 6f) / EdgePx;
        Vector2 tip = new Vector2(tipX, 2f) / EdgePx;
        System.Func<int, int, bool> inside = (x, y) => Inside(new Vector2(x + 0.5f, y + 0.5f), a, b, tip);
        for (int y = 0; y < TailH / EdgePx; y++)
        for (int x = 0; x < TailW / EdgePx; x++)
        {
            if (!inside(x, y)) continue;
            bool edge = !inside(x - 1, y) || !inside(x + 1, y) || !inside(x, y - 1) || !inside(x, y + 1);
            for (int dy = 0; dy < EdgePx; dy++)
            for (int dx = 0; dx < EdgePx; dx++)
                pixels[(y * EdgePx + dy) * TailW + x * EdgePx + dx] = edge ? ArtLine : ArtFill;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, TailW, TailH), TailBase, 100f, 0, SpriteMeshType.FullRect);
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

    // White light, brightest along the edge of a card-cornered box (the
    // card's edge): fading out over GlowSpread around it, and over a short
    // rim inside, where it spills onto the card. Clear in the middle.
    public static Sprite Glow
    {
        get
        {
            if (_glow != null) return _glow;
            const int size = 144;
            var tex = NewTexture(size, size, "CardGlow");
            var pixels = new Color32[size * size];
            Vector2 half = new Vector2(size / 2f - GlowPx, size / 2f - GlowPx);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                float d = RoundedBox(p, half, GlowRadiusPx);
                float light = 1f - Mathf.Clamp01(d > 0f ? d / GlowPx : -d / GlowRimPx);
                pixels[y * size + x] = new Color(1f, 1f, 1f, light * light);
            }
            return _glow = Finish(tex, pixels, GlowPx + GlowRimPx + 4);
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

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
        bool neg = s1 < 0 || s2 < 0 || s3 < 0, pos = s1 > 0 || s2 > 0 || s3 > 0;
        return !(neg && pos);
    }

    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
}
