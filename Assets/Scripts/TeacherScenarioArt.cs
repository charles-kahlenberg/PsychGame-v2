using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// the classroom layers over its Background (the elementary teacher): the
// behaviour board, shapes poster, WELCOME banner and drawings float gently,
// a slower, smaller take on ChocolateScenarioArt's bar, each to its own beat.
public class TeacherScenarioArt : MonoBehaviour
{
    // Each layer, where it was cut from the 960x540 art (pixels from the top
    // left), the point it turns about, how far it turns and drifts sideways.
    // The banner runs off the right edge, so it turns about where it leaves
    // the frame and only drifts up and down, keeping its cut end out of sight.
    static readonly (string name, Rect rect, Vector2 pivot, float degrees, float drift)[] Floaters =
    {
        ("Board", new Rect(27, 24, 159, 384), new Vector2(106, 216), 0.8f, 3f),
        ("ShapesPoster", new Rect(213, 36, 246, 264), new Vector2(336, 168), 1.2f, 3f),
        ("Banner", new Rect(483, 0, 477, 240), new Vector2(960, 150), 1f, 0f),
        ("Drawing1", new Rect(504, 189, 87, 105), new Vector2(547, 241), 2.5f, 3f),
        ("Drawing2", new Rect(621, 234, 102, 135), new Vector2(672, 301), 2.5f, 3f),
        ("Drawing3", new Rect(726, 189, 108, 129), new Vector2(780, 253), 2.5f, 3f),
        ("Drawing4", new Rect(855, 255, 87, 132), new Vector2(898, 321), 2.5f, 3f),
    };
    const float Pace = 0.5f;  // half the chocolate bar's speed
    const float Bob = 4f;     // art pixels up and down

    RectTransform picture;
    RectTransform[] floaters;
    float[] phases, rates;

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        if (Resources.Load<Sprite>(folder + "Board") == null || Resources.Load<Sprite>(folder + "Banner") == null) return;

        picture.gameObject.AddComponent<RectMask2D>(); // the banner runs off the right edge
        var art = picture.gameObject.AddComponent<TeacherScenarioArt>();
        art.picture = picture;
        art.floaters = new RectTransform[Floaters.Length];
        art.phases = new float[Floaters.Length];
        art.rates = new float[Floaters.Length];
        for (int i = 0; i < Floaters.Length; i++)
        {
            var (name, rect, pivot, _, _) = Floaters[i];
            RectTransform layer = Layer(picture, Resources.Load<Sprite>(folder + name), rect);
            layer.pivot = new Vector2((pivot.x - rect.xMin) / rect.width, 1f - (pivot.y - rect.yMin) / rect.height);
            art.floaters[i] = layer;
            art.phases[i] = Random.value * 100f;
            art.rates[i] = Random.Range(0.85f, 1.15f);
        }
    }

    static RectTransform Layer(RectTransform parent, Sprite sprite, Rect rect)
    {
        var layer = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.anchorMin = new Vector2(rect.xMin / 960f, 1f - rect.yMax / 540f);
        layer.anchorMax = new Vector2(rect.xMax / 960f, 1f - rect.yMin / 540f);
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        var image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return layer;
    }

    void Update()
    {
        if (floaters == null) return;
        float scale = picture.rect.width / 960f; // UI units per art pixel
        for (int i = 0; i < floaters.Length; i++)
        {
            var (_, _, _, degrees, drift) = Floaters[i];
            float t = Time.time * Pace * rates[i] + phases[i];
            // A loose loop, turning and pulsing a touch, as the chocolate bar does.
            floaters[i].anchoredPosition = new Vector2(drift * Mathf.Sin(t * 1.1f), -Bob * Mathf.Sin(t * 1.7f)) * scale;
            floaters[i].localEulerAngles = new Vector3(0f, 0f, degrees * Mathf.Sin(t * 0.8f));
            floaters[i].localScale = Vector3.one * (1f + 0.01f * Mathf.Sin(t * 2.3f));
        }
    }
}
