using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.TrophyEnd: the end screen's picture gives way to the shader
// background, excited, in black and white (ShaderBackground.Mono), with the
// pixel-art trophy (Resources/GoldTrophy) floating in the middle like the
// chocolate bar's magic float, and a ring of card backs circling round it,
// one of each area's, evenly spaced, each one's top leading.
//
// The title and line painted into the old picture are written on top.
public class TrophyEnd : MonoBehaviour
{
    const string SceneName = "GameEndScene";
    const float LapSeconds = 22f;

    // Sizes, in stage heights. The ring's radius (to the cards' middles)
    // keeps them clear of the trophy's handles and base, and of the title.
    const float TrophyHeight = 0.66f, CardHeight = 0.2f, RingRadius = 0.445f;

    class Card
    {
        public RectTransform rect;
        public float aspect, phase;
    }

    RectTransform stage, trophy;
    float trophyAspect;
    readonly List<Card> cards = new List<Card>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.TrophyEnd)) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name != SceneName) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<Canvas>() != null) root.AddComponent<TrophyEnd>();
        };
    }

    // In Start, once ShaderBackground has had its turn: without the shader
    // the old picture stays as it was.
    void Start()
    {
        Transform picture = transform.Find("Image");
        Sprite trophySprite = Resources.Load<Sprite>("GoldTrophy");
        CardArt art = CardArt.Load();
        if (GetComponentInChildren<ShaderBackground>() == null || picture == null || trophySprite == null ||
            art == null || art.areas == null || art.areas.Length == 0)
        {
            Destroy(this);
            return;
        }
        picture.GetComponent<Image>().enabled = false;

        stage = new GameObject("TrophyStage", typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(transform, false);
        stage.SetSiblingIndex(picture.GetSiblingIndex() + 1); // under the Menu button
        stage.anchorMin = new Vector2(0.5f, 0.07f);
        stage.anchorMax = new Vector2(0.5f, 0.83f);
        stage.offsetMin = stage.offsetMax = Vector2.zero;

        trophy = Layer(stage, trophySprite).rectTransform;
        trophyAspect = trophySprite.rect.width / trophySprite.rect.height;

        // One of each.
        int count = art.areas.Length;
        for (int i = 0; i < count; i++)
        {
            CardArt.AreaArt area = art.areas[i];
            var card = new Card { rect = CardBack(stage, area.backLayers), phase = (float)i / count };
            Sprite shape = area.backLayers[0].sprite;
            card.aspect = shape.rect.width / shape.rect.height;
            cards.Add(card);
        }

        Caption("Congratulations!", new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.99f), 44f);
        Caption("You completed all the scenarios!", new Vector2(0.2f, 0.005f), new Vector2(0.8f, 0.075f), 20f);
        Update();
    }

    static Image Layer(Transform parent, Sprite sprite)
    {
        var image = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // A card's back: its layers stacked over the whole card.
    static RectTransform CardBack(Transform parent, CardArt.Layer[] layers)
    {
        var card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
        card.SetParent(parent, false);
        foreach (CardArt.Layer layer in layers)
            if (layer != null && layer.sprite != null) Stretch(Layer(card, layer.sprite).rectTransform);
        return card;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void Caption(string words, Vector2 anchorMin, Vector2 anchorMax, float size)
    {
        var text = new GameObject(words, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(transform, false);
        text.rectTransform.SetSiblingIndex(stage.GetSiblingIndex() + 1);
        text.rectTransform.anchorMin = anchorMin;
        text.rectTransform.anchorMax = anchorMax;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        if (MenuButtonPlates.Font != null) text.font = MenuButtonPlates.Font;
        text.text = words;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = size;
        text.color = new Color32(0xFF, 0xF4, 0xD6, 0xFF);
        text.outlineColor = new Color32(0x4A, 0x2C, 0x08, 0xFF);
        text.outlineWidth = 0.25f;
        text.raycastTarget = false;
    }

    void Update()
    {
        if (stage == null) return; // gave up in Start
        float t = Time.time, h = stage.rect.height;

        // The trophy drifts in a loose loop, turning and pulsing a touch.
        Vector2 middle = new Vector2(0.008f * Mathf.Sin(t * 1.1f), 0.012f * Mathf.Sin(t * 1.7f)) * h;
        trophy.anchoredPosition = middle;
        trophy.sizeDelta = new Vector2(trophyAspect, 1f) * TrophyHeight * h;
        trophy.localEulerAngles = new Vector3(0f, 0f, 2f * Mathf.Sin(t * 0.8f));
        trophy.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(t * 2.3f));

        // The ring goes round anticlockwise, each card's top leading.
        foreach (Card c in cards)
        {
            float a = 2f * Mathf.PI * (t / LapSeconds + c.phase);
            c.rect.anchoredPosition = middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius * h;
            c.rect.sizeDelta = new Vector2(c.aspect, 1f) * CardHeight * h;
            c.rect.localEulerAngles = new Vector3(0f, 0f, a * Mathf.Rad2Deg);
        }
    }
}
