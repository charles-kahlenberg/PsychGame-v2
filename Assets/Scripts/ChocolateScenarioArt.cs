using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// the chocolate layers over its Background (the chocolate one): chocolate
// pieces (each drawn twice) and dopamine/serotonin molecules rain down behind
// him at a steady rate, mostly either side of him, the bar in his hands floats
// like a magic object and he sways a little. The first frame is the art as
// drawn.
public class ChocolateScenarioArt : MonoBehaviour
{
    // Where each layer was cut from the 960x540 art, in pixels from the top left.
    static readonly Rect CharacterRect = new Rect(306, 9, 345, 531);
    static readonly Rect ChocolateRect = new Rect(384, 288, 189, 174);
    static readonly Rect[] RainRects =
    {
        new Rect(831, 402, 72, 69), new Rect(201, 21, 117, 126), new Rect(150, 240, 75, 132),
        new Rect(33, 375, 117, 135), new Rect(690, 237, 105, 108),
    };
    static readonly Rect[] MoleculeRects =
    {
        new Rect(648, 318, 201, 165), new Rect(93, 315, 201, 165),
        new Rect(51, 147, 168, 84), new Rect(699, 81, 168, 84),
    };
    // Inside the background's black frame: the rain stays in it, he and his
    // bar (drawn over the frame) are clipped at the picture's edge.
    static readonly Rect Inner = new Rect(12, 9, 936, 519);
    const float DropsPerSecond = 4f;        // pieces let in at the top, evenly spaced
    const float ChocolateBesideHim = 0.85f; // chance a piece comes in to his left or right
    const float MoleculeBesideHim = 0.95f;

    class Piece
    {
        public RectTransform rect;
        public Rect home;
        public int kind;       // which art it is; a chocolate and its twin share one
        public float besideHim;
        public bool falling;
        public Vector2 offset; // from home, art pixels, y down
        public float speed, spin, angle;
    }

    RectTransform picture, character, chocolate;
    readonly List<Piece> rain = new List<Piece>();
    readonly List<Piece> waiting = new List<Piece>(); // above the frame, for their turn
    float nextDrop;

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        Sprite characterSprite = Resources.Load<Sprite>(folder + "Character");
        Sprite chocolateSprite = Resources.Load<Sprite>(folder + "Chocolate");
        if (characterSprite == null || chocolateSprite == null) return;

        picture.gameObject.AddComponent<RectMask2D>();
        var art = picture.gameObject.AddComponent<ChocolateScenarioArt>();
        art.picture = picture;

        var inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
        inner.SetParent(picture, false);
        Anchor(inner, Inner, new Rect(0, 0, 960, 540));
        inner.gameObject.AddComponent<RectMask2D>();

        // Back to front: chocolate rain, molecules, him, the bar in his hands.
        // The drawn pieces start where they are in the art and fall from
        // there; the chocolates' twins wait above for the dropper.
        for (int i = 0; i < RainRects.Length; i++)
        {
            Sprite sprite = Resources.Load<Sprite>(folder + "Rain" + (i + 1));
            art.Spawn(inner, sprite, RainRects[i], i, ChocolateBesideHim, true);
            art.Spawn(inner, sprite, RainRects[i], i, ChocolateBesideHim, false);
        }
        for (int i = 0; i < MoleculeRects.Length; i++)
            art.Spawn(inner, Resources.Load<Sprite>(folder + "Molecule" + (i + 1)), MoleculeRects[i], RainRects.Length + i, MoleculeBesideHim, true);

        // Pivot at his waist, so he sways and breathes from the bottom of the frame.
        art.character = Layer(picture, characterSprite, CharacterRect, new Rect(0, 0, 960, 540));
        art.character.pivot = new Vector2(0.5f, 0f);
        art.chocolate = Layer(picture, chocolateSprite, ChocolateRect, new Rect(0, 0, 960, 540));
    }

    void Spawn(RectTransform inner, Sprite sprite, Rect home, int kind, float besideHim, bool falling)
    {
        var p = new Piece { rect = Layer(inner, sprite, home, Inner), home = home, kind = kind, besideHim = besideHim, falling = falling };
        p.speed = Random.Range(200f, 400f);
        p.spin = Random.Range(-25f, 25f);
        rain.Add(p);
        if (!falling)
        {
            p.offset = new Vector2(0f, -1000f); // out of sight above until dropped
            waiting.Add(p);
        }
    }

    static RectTransform Layer(RectTransform parent, Sprite sprite, Rect rect, Rect parentRect)
    {
        var layer = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        Anchor(layer, rect, parentRect);
        var image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return layer;
    }

    // Stretches rect over where it sits in parentRect (both in art pixels, y down).
    static void Anchor(RectTransform layer, Rect rect, Rect parentRect)
    {
        layer.anchorMin = new Vector2((rect.xMin - parentRect.xMin) / parentRect.width, 1f - (rect.yMax - parentRect.yMin) / parentRect.height);
        layer.anchorMax = new Vector2((rect.xMax - parentRect.xMin) / parentRect.width, 1f - (rect.yMin - parentRect.yMin) / parentRect.height);
        layer.offsetMin = layer.offsetMax = Vector2.zero;
    }

    static float Reach(Piece p) => p.home.size.magnitude / 2f; // clear of the frame however it's turned

    void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        float scale = picture.rect.width / 960f; // UI units per art pixel

        // The rain: each piece falls out the bottom and waits above for its
        // turn. The dropper lets one in every 1/DropsPerSecond (give or take),
        // so the frame always has about the same amount falling, and each
        // comes in somewhere new at its own speed and spin.
        foreach (Piece p in rain)
        {
            if (p.falling)
            {
                p.offset.y += p.speed * dt;
                p.angle += p.spin * dt;
                if (p.home.center.y + p.offset.y - Reach(p) > Inner.yMax)
                {
                    p.falling = false;
                    waiting.Add(p);
                }
            }
            Place(p.rect, p.offset, p.angle, scale); // waiting ones sit out of sight
        }
        if (t >= nextDrop && waiting.Count > 0)
        {
            Drop(NextInLine());
            nextDrop = t + Random.Range(0.85f, 1.15f) / DropsPerSecond;
        }

        // The bar drifts in a loose loop over his hands, turning and pulsing a touch.
        Place(chocolate, new Vector2(5f * Mathf.Sin(t * 1.1f), 6f * Mathf.Sin(t * 1.7f)), 4f * Mathf.Sin(t * 0.8f), scale);
        chocolate.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 2.3f));

        // He breathes (about 4.5s a breath) and sways slowly.
        float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 4.5f);
        character.localScale = new Vector3(1f + 0.004f * breath, 1f + 0.01f * breath, 1f);
        character.localEulerAngles = new Vector3(0f, 0f, 0.6f * Mathf.Sin(t * Mathf.PI * 2f / 6.5f));
    }

    // A random waiting piece, but not one whose twin (same art) is still in
    // the top part of the frame, so twins don't fall together.
    Piece NextInLine()
    {
        var ready = waiting.FindAll(w => !rain.Exists(p => p.falling && p.kind == w.kind && p.home.center.y + p.offset.y < Inner.yMin + 260f));
        List<Piece> from = ready.Count > 0 ? ready : waiting;
        return from[Random.Range(0, from.Count)];
    }

    void Drop(Piece p)
    {
        waiting.Remove(p);
        p.falling = true;
        p.offset = new Vector2(OpenColumn(p), Inner.yMin - Reach(p)) - p.home.center;
        p.angle = Random.Range(0f, 360f);
        p.speed = Random.Range(200f, 400f);
        p.spin = Random.Range(-25f, 25f);
    }

    // A random x for a piece coming in: usually to his left or right (its
    // besideHim chance), kept far enough out that most of it clears him, now
    // and then anywhere; and off any piece still near the top so they don't
    // fall in clumps (gives up after a few tries).
    float OpenColumn(Piece piece)
    {
        float half = piece.home.width / 2f, x = 0f;
        for (int tries = 0; tries < 6; tries++)
        {
            if (Random.value < piece.besideHim)
                x = Random.value < 0.5f
                    ? Random.Range(Inner.xMin + 40f, CharacterRect.xMin - half * 0.5f)
                    : Random.Range(CharacterRect.xMax + half * 0.5f, Inner.xMax - 40f);
            else
                x = Random.Range(Inner.xMin + 40f, Inner.xMax - 40f);
            bool clear = true;
            foreach (Piece p in rain)
            {
                Vector2 c = p.home.center + p.offset;
                if (p != piece && p.falling && c.y < Inner.yMin + 150f && Mathf.Abs(c.x - x) < 110f) clear = false;
            }
            if (clear) break;
        }
        return x;
    }

    static void Place(RectTransform rect, Vector2 offset, float angle, float scale)
    {
        rect.anchoredPosition = new Vector2(offset.x, -offset.y) * scale;
        rect.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}
