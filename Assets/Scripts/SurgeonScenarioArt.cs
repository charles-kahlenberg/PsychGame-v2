using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// LeftHand, RightHand and Blink layers over its Background (the sleep
// deprived surgeon): her hands tremble, the lights flicker on the room and her
// hands, and her eyes (Blink as the upper lid, flipped for the lower) drag
// half shut now and then before she blinks herself awake.
public class SurgeonScenarioArt : MonoBehaviour
{
    // Where each hand was cut from the 960x540 art, in pixels from the top
    // left, and the point below the frame each trembles about (her elbow).
    static readonly Rect LeftHandRect = new Rect(72, 174, 354, 366);
    static readonly Rect RightHandRect = new Rect(528, 87, 432, 453);
    static readonly Vector2 LeftElbow = new Vector2(200, 600);
    static readonly Vector2 RightElbow = new Vector2(930, 600);

    const float TremorDegrees = 1.2f; // the scalpel's tip moves a few pixels
    const float TremorRate = 7f;      // wobbles a second, roughly

    RectTransform picture, leftHand, rightHand, upperLid, lowerLid;
    Image[] lit; // the background and hands, dimmed by the flickering lights
    float closed; // 0 eyes open .. 1 shut
    float brightness = 1f; // the lights' brightness, 1 steady

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        Sprite left = Resources.Load<Sprite>(folder + "LeftHand");
        Sprite right = Resources.Load<Sprite>(folder + "RightHand");
        Sprite blink = Resources.Load<Sprite>(folder + "Blink");
        if (left == null || right == null || blink == null) return;

        picture.gameObject.AddComponent<RectMask2D>(); // the lids wait outside the frame
        var art = picture.gameObject.AddComponent<SurgeonScenarioArt>();
        art.picture = picture;
        art.leftHand = Hand(picture, left, LeftHandRect, LeftElbow);
        art.rightHand = Hand(picture, right, RightHandRect, RightElbow);
        art.upperLid = Layer(picture, blink, new Rect(0, 0, 960, 540));
        art.lowerLid = Layer(picture, blink, new Rect(0, 0, 960, 540));
        art.lowerLid.localScale = new Vector3(1f, -1f, 1f);
        art.lit = new[] { picture.GetComponent<Image>(), art.leftHand.GetComponent<Image>(), art.rightHand.GetComponent<Image>() };
    }

    static RectTransform Hand(RectTransform picture, Sprite sprite, Rect rect, Vector2 elbow)
    {
        RectTransform hand = Layer(picture, sprite, rect);
        hand.pivot = new Vector2((elbow.x - rect.xMin) / rect.width, 1f - (elbow.y - rect.yMin) / rect.height);
        return hand;
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

    void OnEnable()
    {
        closed = 0f;
        brightness = 1f;
        StartCoroutine(Blinking());
        StartCoroutine(Flickering());
    }

    void Update()
    {
        if (picture == null) return;
        float t = Time.time;
        float scale = picture.rect.width / 960f; // UI units per art pixel

        leftHand.localEulerAngles = new Vector3(0f, 0f, TremorDegrees * Tremor(t, 0.3f));
        rightHand.localEulerAngles = new Vector3(0f, 0f, TremorDegrees * Tremor(t, 7.9f));

        // A faint buzz under the flickers.
        float b = brightness * (1f - 0.05f * Mathf.PerlinNoise(t * 12f, 3.3f));
        foreach (Image image in lit) image.color = new Color(b, b, b, image.color.a);

        // Open, the lids sit just outside the frame; shut, each covers its half.
        float away = (1f - closed) * 540f * scale;
        upperLid.anchoredPosition = new Vector2(0f, away);
        lowerLid.anchoredPosition = new Vector2(0f, -away);
    }

    // Smooth noise in -1..1, a different stream for each seed.
    static float Tremor(float t, float seed) => Mathf.PerlinNoise(t * TremorRate, seed) * 2f - 1f;

    // Steady lights for a while, then one of a few kinds of flicker. They only
    // ever dim the room partway (never below 0.65), so they can't be mistaken
    // for her blinking.
    IEnumerator Flickering()
    {
        while (true)
        {
            brightness = 1f;
            yield return new WaitForSeconds(Random.Range(0.5f, 3.5f));
            switch (Random.Range(0, 4))
            {
                case 0: // stutter: a few sharp dips with brief returns between
                    for (int i = Random.Range(2, 6); i > 0; i--)
                    {
                        brightness = Random.Range(0.65f, 0.85f);
                        yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
                        brightness = Random.Range(0.92f, 1f);
                        yield return new WaitForSeconds(Random.Range(0.04f, 0.18f));
                    }
                    break;
                case 1: // flutter: a fast, shallow shimmer
                    for (float end = Time.time + Random.Range(0.3f, 1.2f); Time.time < end;)
                    {
                        brightness = Random.Range(0.82f, 1f);
                        yield return new WaitForSeconds(Random.Range(0.02f, 0.05f));
                    }
                    break;
                case 2: // sag: the lights brown out and creep back
                    float low = Random.Range(0.7f, 0.85f), down = Random.Range(0.15f, 0.4f), up = Random.Range(0.3f, 0.8f);
                    for (float s = 0f; s < down; s += Time.deltaTime) { brightness = Mathf.Lerp(1f, low, s / down); yield return null; }
                    for (float s = 0f; s < up; s += Time.deltaTime) { brightness = Mathf.Lerp(low, 1f, s / up); yield return null; }
                    break;
                default: // pop: one quick dip
                    brightness = Random.Range(0.68f, 0.8f);
                    yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
                    break;
            }
        }
    }

    // Awake a few seconds; then the lids drag down slowly, past halfway, and
    // the moment they get there she blinks out of it, sometimes a couple more
    // times quickly.
    IEnumerator Blinking()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(2f, 4.5f));

            // About 0.57 shut the lids' ragged edges cover half the picture.
            // The drag speeds up into the blink rather than settling first.
            float droop = Random.Range(0.58f, 0.66f);
            yield return Lids(0f, droop, Random.Range(1.5f, 3f), true);
            yield return Lids(droop, 1f, 0.05f);
            yield return Lids(1f, 0f, 0.09f);

            for (int i = Random.Range(0, 3); i > 0; i--)
            {
                yield return new WaitForSeconds(Random.Range(0.08f, 0.3f));
                yield return Lids(0f, 1f, 0.07f);
                yield return Lids(1f, 0f, 0.09f);
            }
        }
    }

    // Eases in and out, or with drag only in (still moving when it arrives).
    IEnumerator Lids(float from, float to, float seconds, bool drag = false)
    {
        for (float s = 0f; s < seconds; s += Time.deltaTime)
        {
            float f = s / seconds;
            closed = Mathf.Lerp(from, to, drag ? 1f - Mathf.Cos(f * Mathf.PI / 2f) : Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }
        closed = to;
    }
}
