using UnityEngine;
using UnityEngine.UI;

// Feature.ScenarioImage, for a scenario whose ScenarioImages/<n>/ folder has
// Phone and Character layers over its Background (the social media one):
// the girl breathes and the phone buzzes on the pillow every few seconds.
public class PhoneScenarioArt : MonoBehaviour
{
    // Where Phone.png was cut from the 960x540 art, in pixels from the top left.
    static readonly Rect PhoneRect = new Rect(736, 136, 109, 139);

    RectTransform phone, character;
    float nextBuzz, buzzStart = -10f;

    // Adds the layers over the picture (whose sprite is the background), if
    // the folder has them.
    public static void Add(RectTransform picture, string folder)
    {
        Sprite phoneSprite = Resources.Load<Sprite>(folder + "Phone");
        Sprite characterSprite = Resources.Load<Sprite>(folder + "Character");
        if (phoneSprite == null || characterSprite == null) return;

        picture.gameObject.AddComponent<RectMask2D>(); // her breathing stays inside the frame
        var art = picture.gameObject.AddComponent<PhoneScenarioArt>();

        art.phone = Layer(picture, phoneSprite,
            new Vector2(PhoneRect.xMin / 960f, 1f - PhoneRect.yMax / 540f),
            new Vector2(PhoneRect.xMax / 960f, 1f - PhoneRect.yMin / 540f));
        art.phone.pivot = new Vector2(0.5f, 0.5f);

        // Pivot low on her back, so breathing lifts her shoulders and head.
        art.character = Layer(picture, characterSprite, Vector2.zero, Vector2.one);
        art.character.pivot = new Vector2(0.2f, 0f);

        art.nextBuzz = Time.time + 1.5f;
    }

    static RectTransform Layer(RectTransform parent, Sprite sprite, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return rect;
    }

    void Update()
    {
        float t = Time.time;

        // Two hard buzzes, like a notification, then quiet for a few seconds.
        if (t >= nextBuzz)
        {
            buzzStart = t;
            nextBuzz = t + Random.Range(2.5f, 5f);
        }
        float s = t - buzzStart;
        bool buzzing = s < 0.4f || (s > 0.55f && s < 0.95f);
        float width = ((RectTransform)transform).rect.width;
        phone.anchoredPosition = buzzing ? Random.insideUnitCircle * width * 0.012f : Vector2.zero;
        phone.localEulerAngles = new Vector3(0f, 0f, buzzing ? Random.Range(-10f, 10f) : 0f);
        phone.localScale = Vector3.one * (buzzing ? 1.06f : 1f);

        // Slow, sleepy breathing (about 4s a breath). Exhaled she fits the
        // picture exactly; inhaling she swells, clipped at the frame.
        float breath = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 4.2f);
        character.localScale = new Vector3(1f + 0.004f * breath, 1f + 0.015f * breath, 1f);
    }
}
