using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// the jury layers over its Background (the eyewitness one): the juror
// breathes and sways in the box while her thought bubble floats like
// ChocolateScenarioArt's bar, and in it the suspects' faces come and go, each
// followed by a question mark: face, ?, next face, ?, round and round.
public class JuryScenarioArt : MonoBehaviour
{
    // Where each layer was cut from the 960x540 art, in pixels from the top left.
    static readonly Rect JurorRect = new Rect(564, 21, 267, 441);
    static readonly Rect BubbleRect = new Rect(30, 36, 399, 468);
    static readonly Rect TrailRect = new Rect(381, 69, 180, 222);
    static readonly Rect[] FaceRects = { new Rect(66, 51, 297, 399), new Rect(99, 78, 270, 348), new Rect(111, 75, 255, 372) };
    static readonly Rect QuestionRect = new Rect(168, 105, 123, 315);
    static readonly Rect Art = new Rect(0, 0, 960, 540);

    const float FaceSeconds = 2.5f;     // each face held in the bubble
    const float QuestionSeconds = 1.2f; // the question mark after it
    const float Fade = 0.3f;            // in or out

    RectTransform picture, juror, bubble, trail;
    Image[] thoughts; // the faces, then the question mark
    float[] phases;

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        Sprite bubbleSprite = Resources.Load<Sprite>(folder + "Bubble");
        Sprite jurorSprite = Resources.Load<Sprite>(folder + "Juror");
        if (bubbleSprite == null || jurorSprite == null) return;

        picture.gameObject.AddComponent<RectMask2D>();
        var art = picture.gameObject.AddComponent<JuryScenarioArt>();
        art.picture = picture;

        // Pivot at her seat, so she breathes and sways from the box's rim.
        art.juror = Layer(picture, jurorSprite, JurorRect, Art);
        art.juror.pivot = new Vector2(0.5f, 0f);
        art.trail = Layer(picture, Resources.Load<Sprite>(folder + "BubbleTrail"), TrailRect, Art);
        art.bubble = Layer(picture, bubbleSprite, BubbleRect, Art);

        // The thoughts ride in the bubble; only the first face shows to begin with.
        art.thoughts = new Image[FaceRects.Length + 1];
        for (int i = 0; i < FaceRects.Length; i++)
            art.thoughts[i] = Layer(art.bubble, Resources.Load<Sprite>(folder + "Face" + (i + 1)), FaceRects[i], BubbleRect).GetComponent<Image>();
        art.thoughts[FaceRects.Length] = Layer(art.bubble, Resources.Load<Sprite>(folder + "Question"), QuestionRect, BubbleRect).GetComponent<Image>();

        art.phases = new float[art.thoughts.Length + 2];
        for (int i = 0; i < art.phases.Length; i++) art.phases[i] = Random.value * 100f;
        art.OnEnable(); // the layers weren't there when Unity called it
    }

    void OnEnable()
    {
        if (thoughts != null && isActiveAndEnabled) StartCoroutine(Thinking());
    }

    static RectTransform Layer(RectTransform parent, Sprite sprite, Rect rect, Rect parentRect)
    {
        var layer = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        layer.SetParent(parent, false);
        layer.anchorMin = new Vector2((rect.xMin - parentRect.xMin) / parentRect.width, 1f - (rect.yMax - parentRect.yMin) / parentRect.height);
        layer.anchorMax = new Vector2((rect.xMax - parentRect.xMin) / parentRect.width, 1f - (rect.yMin - parentRect.yMin) / parentRect.height);
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        var image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return layer;
    }

    static void SetAlpha(Image image, float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }

    void Update()
    {
        float t = Time.time;
        float scale = picture.rect.width / 960f; // UI units per art pixel

        // The bubble drifts in a loose loop like the chocolate bar, turning
        // less as it's much bigger; the little trail of bubbles to its own beat.
        Float(bubble, t * 0.6f + phases[0], 5f, 6f, 1.5f, 0.015f, scale);
        Float(trail, t * 0.8f + phases[1], 3f, 4f, 2f, 0.02f, scale);

        // The faces and question mark bob gently inside it.
        for (int i = 0; i < thoughts.Length; i++)
            Float(thoughts[i].rectTransform, t * 0.7f + phases[i + 2], 3f, 4f, 2f, 0f, scale);

        // She breathes (about 4.5s a breath) and sways slowly, as the chocolate man does.
        float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 4.5f);
        juror.localScale = new Vector3(1f + 0.004f * breath, 1f + 0.01f * breath, 1f);
        juror.localEulerAngles = new Vector3(0f, 0f, 0.4f * Mathf.Sin(t * Mathf.PI * 2f / 6.5f));
    }

    // ChocolateScenarioArt's float: a loose loop, turning and pulsing a touch.
    // A pulse of 0 leaves the scale alone (the thoughts' is Thinking's pop-in).
    static void Float(RectTransform rect, float t, float driftX, float driftY, float degrees, float pulse, float scale)
    {
        rect.anchoredPosition = new Vector2(driftX * Mathf.Sin(t * 1.1f), -driftY * Mathf.Sin(t * 1.7f)) * scale;
        rect.localEulerAngles = new Vector3(0f, 0f, degrees * Mathf.Sin(t * 0.8f));
        if (pulse > 0f) rect.localScale = Vector3.one * (1f + pulse * Mathf.Sin(t * 2.3f));
    }

    // Face, question mark, next face, question mark... Each fades in growing
    // slightly into place, holds, and fades out before the next. Starts (and
    // restarts, if the picture is hidden and shown again) on the first face.
    IEnumerator Thinking()
    {
        for (int i = 0; i < thoughts.Length; i++)
        {
            SetAlpha(thoughts[i], i == 0 ? 1f : 0f);
            thoughts[i].rectTransform.localScale = Vector3.one;
        }
        Image question = thoughts[thoughts.Length - 1];
        for (int face = 0; ; face = (face + 1) % (thoughts.Length - 1))
        {
            if (face != 0 || thoughts[0].color.a < 1f) yield return Show(thoughts[face], true);
            yield return new WaitForSeconds(FaceSeconds);
            yield return Show(thoughts[face], false);
            yield return Show(question, true);
            yield return new WaitForSeconds(QuestionSeconds);
            yield return Show(question, false);
        }
    }

    IEnumerator Show(Image image, bool appear)
    {
        for (float s = 0f; s < Fade; s += Time.deltaTime)
        {
            float f = appear ? s / Fade : 1f - s / Fade;
            SetAlpha(image, f);
            image.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }
        SetAlpha(image, appear ? 1f : 0f);
        image.rectTransform.localScale = Vector3.one;
    }
}
