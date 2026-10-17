using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// the meditation layers over its Background (the mindfulness one): her bangs
// and shoulder locks stream in a wind blowing to the right, in gusts, each
// fluttering about where it meets her head, leaves spin past her on the wind
// at random intervals and speeds, like ChocolateScenarioArt's rain, and she
// slowly levitates off the grass and hovers as her meditation deepens.
public class MeditationScenarioArt : MonoBehaviour
{
    // Where each layer was cut from the 960x540 art, in pixels from the top left.
    static readonly Rect GlowRect = new Rect(75, 0, 798, 462);
    static readonly Rect BodyRect = new Rect(246, 105, 510, 435);
    static readonly Rect[] LeafRects = { new Rect(798, 255, 78, 33), new Rect(147, 231, 48, 54), new Rect(69, 141, 84, 27) };
    // The strands that blow: name, where it sits (pixels from the art's top
    // left), where it meets the head, where its tip is (which way the wind
    // pushes it), how much it moves, and whether it's a shoulder lock, which
    // goes over her shoulders but under Head. The shoulder locks sit 3px in
    // toward her neck from where they were cut, closing the light gaps there.
    static readonly (string name, Rect rect, Vector2 root, Vector2 tip, float give, bool underHead)[] Strands =
    {
        ("Hair1", new Rect(411, 189, 57, 102), new Vector2(443, 192), new Vector2(467, 290), 1.8f, true), // left shoulder
        ("Hair4", new Rect(453, 96, 105, 72), new Vector2(480, 149), new Vector2(557, 164), 2.2f, false),  // bangs
        ("Hair5", new Rect(447, 108, 96, 72), new Vector2(472, 160), new Vector2(542, 179), 2.2f, false),  // bangs
        ("Hair7", new Rect(483, 171, 99, 135), new Vector2(501, 171), new Vector2(533, 305), 1.8f, true), // right shoulder
    };
    // Her head and neck (everything of Body above the shoulders) with the hair
    // that stays still (the top and the little bits on the left) baked in as
    // one image, so no seams show between them when the picture is scaled
    // down; drawn over the shoulder locks so they hang behind it.
    static readonly Rect HeadRect = new Rect(405, 81, 117, 141);
    // Inside the background's black frame: the leaves stay in it.
    static readonly Rect Inner = new Rect(9, 9, 939, 519);

    const float WindDegrees = 4f;    // how far a gust pushes a hanging strand's tip right
    const float FlutterDegrees = 2f; // the quick flapping on top
    const float GustRate = 0.4f;     // gusts build and ease over a few seconds
    const float FlutterRate = 3f;    // flaps a second, roughly

    const float RiseHeight = 30f;  // art pixels she lifts off the grass
    const float RiseSeconds = 5f;  // slowly, as her meditation deepens
    const float HoverHeight = 5f;  // then she drifts up and down this much
    const float HoverSeconds = 4f; // a breath, roughly

    class Leaf
    {
        public RectTransform rect;
        public Rect home;
        public bool flying;
        public Vector2 offset; // from home, art pixels, y down
        public float speed, spin, angle, bob, bobRate, phase;
    }

    RectTransform picture, figure;
    RectTransform[] strands;
    readonly List<Leaf> leaves = new List<Leaf>();
    readonly List<Leaf> waiting = new List<Leaf>(); // off the left edge, for their turn
    float nextLeaf, start;

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        Sprite body = Resources.Load<Sprite>(folder + "Body");
        if (body == null || Resources.Load<Sprite>(folder + "Hair1") == null) return;

        picture.gameObject.AddComponent<RectMask2D>();
        var art = picture.gameObject.AddComponent<MeditationScenarioArt>();
        art.picture = picture;
        var whole = new Rect(0, 0, 960, 540);

        Layer(picture, Resources.Load<Sprite>(folder + "Glow"), GlowRect, whole);

        // Back to front: glow, leaves, her, her hair. The drawn leaves start
        // where they are in the art and fly off from there; a second of each
        // waits off the left edge.
        var inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
        inner.SetParent(picture, false);
        Anchor(inner, Inner, whole);
        inner.gameObject.AddComponent<RectMask2D>();
        for (int i = 0; i < LeafRects.Length; i++)
        {
            Sprite leaf = Resources.Load<Sprite>(folder + "Leaf" + (i + 1));
            art.Spawn(inner, leaf, LeafRects[i], true);
            art.Spawn(inner, leaf, LeafRects[i], false);
        }

        // All of her in one group, so she levitates as one: the body, the
        // shoulder locks over it, her head over their tops, then the bangs.
        art.figure = new GameObject("Figure", typeof(RectTransform)).GetComponent<RectTransform>();
        art.figure.SetParent(picture, false);
        Anchor(art.figure, whole, whole);
        Layer(art.figure, body, BodyRect, whole);
        art.strands = new RectTransform[Strands.Length];
        for (int pass = 0; pass < 2; pass++)
        {
            if (pass == 1) Layer(art.figure, Resources.Load<Sprite>(folder + "Head"), HeadRect, whole);
            for (int i = 0; i < Strands.Length; i++)
            {
                var (name, rect, root, _, _, underHead) = Strands[i];
                if (underHead != (pass == 0)) continue;
                RectTransform strand = Layer(art.figure, Resources.Load<Sprite>(folder + name), rect, whole);
                strand.pivot = new Vector2((root.x - rect.xMin) / rect.width, 1f - (root.y - rect.yMin) / rect.height);
                art.strands[i] = strand;
            }
        }
        art.nextLeaf = Time.time + Random.Range(0.5f, 2f);
        art.start = Time.time;
    }

    void Spawn(RectTransform inner, Sprite sprite, Rect home, bool flying)
    {
        var leaf = new Leaf { rect = Layer(inner, sprite, home, Inner), home = home, flying = flying };
        Restyle(leaf);
        leaves.Add(leaf);
        if (!flying)
        {
            leaf.offset = new Vector2(-2000f, 0f); // out of sight left until let go
            waiting.Add(leaf);
        }
    }

    // A fresh speed, spin and bob for a leaf's next pass.
    static void Restyle(Leaf leaf)
    {
        leaf.speed = Random.Range(450f, 850f);
        leaf.spin = Random.Range(-240f, 240f);
        leaf.bob = Random.Range(8f, 25f);
        leaf.bobRate = Random.Range(1f, 2.5f);
        leaf.phase = Random.value * 100f;
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

    static float Reach(Leaf leaf) => leaf.home.size.magnitude / 2f; // clear of the frame however it's turned

    void Update()
    {
        if (strands == null) return;
        float t = Time.time, dt = Time.deltaTime;
        float scale = picture.rect.width / 960f; // UI units per art pixel

        // She rises slowly off the grass, then hovers, drifting gently up and
        // down; the drift eases in with the rise so there's no jolt.
        float since = t - start;
        float risen = Mathf.SmoothStep(0f, 1f, since / RiseSeconds);
        float height = RiseHeight * risen + HoverHeight * risen * Mathf.Sin(since * Mathf.PI * 2f / HoverSeconds);
        figure.anchoredPosition = new Vector2(0f, height * scale);

        // The wind: one gust strength for all of her, a flutter for each strand.
        // A gust turns a strand so its tip goes right: hanging ones swing out,
        // ones already streaming right mostly flap.
        float gust = Mathf.PerlinNoise(t * GustRate, 2.1f);
        for (int i = 0; i < strands.Length; i++)
        {
            var (_, _, root, tip, give, _) = Strands[i];
            Vector2 along = (tip - root).normalized;
            float flutter = Mathf.PerlinNoise(t * FlutterRate, i * 5.3f) * 2f - 1f;
            float degrees = give * (WindDegrees * gust * along.y + FlutterDegrees * (0.4f + 0.6f * gust) * flutter);
            strands[i].localEulerAngles = new Vector3(0f, 0f, degrees);
        }

        // The leaves: each spins right across the frame with the wind,
        // bobbing, then waits off the left edge until one is let go again at
        // a random moment.
        foreach (Leaf leaf in leaves)
        {
            if (leaf.flying)
            {
                leaf.offset.x += leaf.speed * dt;
                leaf.angle += leaf.spin * dt;
                if (leaf.home.center.x + leaf.offset.x - Reach(leaf) > Inner.xMax)
                {
                    leaf.flying = false;
                    waiting.Add(leaf);
                }
            }
            float lt = t + leaf.phase;
            float bob = leaf.bob * Mathf.Sin(lt * leaf.bobRate * Mathf.PI);
            leaf.rect.anchoredPosition = new Vector2(leaf.offset.x, -(leaf.offset.y + bob)) * scale;
            leaf.rect.localEulerAngles = new Vector3(0f, 0f, leaf.angle);
        }
        if (t >= nextLeaf && waiting.Count > 0)
        {
            Leaf leaf = waiting[Random.Range(0, waiting.Count)];
            waiting.Remove(leaf);
            leaf.flying = true;
            Vector2 start = new Vector2(Inner.xMin - Reach(leaf) - 30f, Random.Range(Inner.yMin + 40f, 420f));
            leaf.offset = start - leaf.home.center;
            leaf.angle = Random.Range(0f, 360f);
            Restyle(leaf);
            nextLeaf = t + Random.Range(0.4f, 2.2f);
        }
    }
}
