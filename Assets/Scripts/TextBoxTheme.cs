using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.ThemedTextBoxes (Group 2): restyles the response screen's text
// boxes to match the paper-layer background, set in Inter (Resources/Fonts)
// for legibility. Built at runtime on GameScene's existing objects, so every
// script that already points at them keeps working:
//  - Bubble Box (the scenario): sized to its text, beside the avatar, with a
//    tail pointing at them. It can't grow into the response box; anything
//    longer scrolls.
//  - HintBubbleContainer (Brainy's hints and the cards' definitions): the
//    same panel with a "Brainy" name tag, centered just under the response
//    box and growing down over the cards, scrollable once it would reach the
//    bottom of the screen. Its tail points at the spot Brainy steps forward
//    to (BrainBehavior.focusPosition) whenever it opens. While it's open the
//    response box stays above the dimmed overlay and can still be clicked
//    and typed in, so a hint can be read against the answer.
//  - ResponseInput: the same paper look, and a scrollbar once an answer is
//    longer than the box (the mouse wheel scrolls it too).
public static class TextBoxTheme
{
    // Colors, in sRGB. The panels are warm paper with a deep indigo edge, like
    // the background's layers; text is near-black ink for full contrast.
    public static readonly Color Paper = new Color32(0xF7, 0xF1, 0xE6, 0xFF);
    public static readonly Color Ink = new Color32(0x1F, 0x22, 0x33, 0xFF);
    public static readonly Color Edge = new Color32(0x2E, 0x35, 0x66, 0xFF);
    public static readonly Color Accent = new Color32(0x4A, 0x54, 0x94, 0xFF);
    private static readonly Color PlaceholderInk = new Color32(0x6B, 0x6F, 0x85, 0xFF);
    private static readonly Color OverlayTint = new Color(0.05f, 0.06f, 0.12f, 0.6f);

    private const string SceneName = "GameScene";

    // Sizes in canvas units (the canvas is 450 tall).
    private const float ScenarioFontSize = 14f;
    private const float BubbleFontSize = 13f;
    private const float ResponseFontSize = 13f;
    private const float ScreenMargin = 12f;
    private const float AvatarGap = 10f;
    private const float HintBubbleWidth = 460f;
    private const float HintBubbleGap = 16f;       // below the response box (the name tag sits in it)
    private const float HintTailFromTop = 30f;     // where the tail leaves the bubble's left edge
    private const float BrainySpeakingScale = 1.3f; // BrainBehavior grows Brainy this much when it speaks

    public static bool Enabled => TestGroups.IsEnabled(Feature.ThemedTextBoxes);

    private static TMP_FontAsset _regular, _semiBold;
    public static TMP_FontAsset Regular => _regular != null ? _regular : _regular = Resources.Load<TMP_FontAsset>("Fonts/Inter-Regular SDF");
    public static TMP_FontAsset SemiBold => _semiBold != null ? _semiBold : _semiBold = Resources.Load<TMP_FontAsset>("Fonts/Inter-SemiBold SDF");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == SceneName) Apply(scene);
        };
    }

    private static void Apply(Scene scene)
    {
        if (Regular == null || SemiBold == null)
        {
            Debug.LogWarning("[TextBoxTheme] Inter font assets missing from Resources/Fonts; leaving the text boxes as they are.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;

            var canvasRect = (RectTransform)canvas.transform;
            var response = canvas.transform.Find("ResponseInput") as RectTransform;

            var overlay = canvas.transform.Find("HintOverlay")?.GetComponent<Image>();
            if (overlay != null) overlay.color = OverlayTint;

            StyleScenario(canvasRect, response);
            StyleHintBubble(canvasRect, response, overlay);
            if (response != null) StyleResponse(response);
        }
    }

    // -------------------- SCENARIO --------------------

    private static void StyleScenario(RectTransform canvas, RectTransform response)
    {
        var box = canvas.Find("Bubble Box") as RectTransform;
        var text = box != null ? box.Find("ScenarioText")?.GetComponent<TextMeshProUGUI>() : null;
        if (text == null) return;

        // From the top-left margin to just short of the avatar.
        var avatar = canvas.Find("Avatar") as RectTransform;
        float rightAnchor = 0.8f, rightOffset = 0f;
        if (avatar != null && avatar.anchorMin == avatar.anchorMax)
        {
            rightAnchor = avatar.anchorMin.x;
            rightOffset = avatar.anchoredPosition.x - avatar.rect.width * avatar.pivot.x - AvatarGap;
        }
        box.anchorMin = new Vector2(0f, 1f);
        box.anchorMax = new Vector2(rightAnchor, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.offsetMin = new Vector2(ScreenMargin, -ScreenMargin - 60f);
        box.offsetMax = new Vector2(rightOffset, -ScreenMargin); // TextPanel sets the height

        // Never tall enough to reach the response box.
        float maxHeight = 180f;
        if (response != null)
        {
            float fromTop = canvas.rect.height / 2f - EdgeY(canvas, response, top: true);
            maxHeight = Mathf.Max(60f, fromTop - ScreenMargin - TextPanel.TailLength - 6f);
        }

        StyleBodyText(text, Regular, ScenarioFontSize);
        var panel = TextPanel.Build(box, text, minHeight: 48f, maxHeight: maxHeight, nameTag: null);
        panel.AddTail(TextPanel.TailSide.Bottom, 0.92f);
    }

    // -------------------- BRAINY'S BUBBLE --------------------

    private static void StyleHintBubble(RectTransform canvas, RectTransform response, Image overlay)
    {
        var bubble = canvas.Find("HintBubbleContainer") as RectTransform;
        if (bubble == null) return;

        var oldScroll = bubble.Find("HintScrollView");
        var text = oldScroll != null ? oldScroll.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (text == null) return;

        // Centered, hanging from just under the response box and growing down
        // over the cards, so it never covers the answer being written.
        float top = (response != null ? EdgeY(canvas, response, top: false) : -60f) - HintBubbleGap;
        float bottom = -canvas.rect.height / 2f + ScreenMargin;
        bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
        bubble.pivot = new Vector2(0.5f, 1f);
        bubble.anchoredPosition = new Vector2(0f, top);
        bubble.sizeDelta = new Vector2(HintBubbleWidth, 80f);

        StyleBodyText(text, Regular, BubbleFontSize);
        var panel = TextPanel.Build(bubble, text, minHeight: 52f, maxHeight: Mathf.Max(80f, top - bottom), nameTag: "Brainy");
        panel.AddTail(TextPanel.TailSide.Left, 1f, -HintTailFromTop);
        oldScroll.gameObject.SetActive(false);

        // While Brainy talks, the answer stays readable and editable: the
        // response box is lifted above the dimmed overlay, and clicking into
        // it doesn't close the bubble.
        if (response != null && overlay != null)
        {
            var raise = bubble.gameObject.AddComponent<RaiseAboveOverlay>();
            raise.target = response;
            raise.overlay = overlay.transform;

            var closer = overlay.GetComponent<OverlayClickCloser>();
            if (closer != null) closer.keepOpenWhenClicked = new[] { response };
        }

        // HintManager scrolls its view back to the top on each new hint.
        var hintManager = Object.FindFirstObjectByType<HintManager>();
        if (hintManager != null) hintManager.hintScrollView = panel.Scroll;

        // Brainy steps forward to just beyond the tail's tip whenever the bubble opens.
        var brain = canvas.Find("Brain");
        var brainBehavior = brain != null ? brain.GetComponent<BrainBehavior>() : null;
        if (brainBehavior != null)
        {
            var brainRect = (RectTransform)brain;
            float halfWidth = brainRect.rect.width * brainRect.localScale.x * BrainySpeakingScale / 2f;
            brainBehavior.focusPosition = new Vector3(
                -HintBubbleWidth / 2f - TextPanel.TailLength - 4f - halfWidth,
                top - HintTailFromTop - TextPanel.TailDrop,
                0f);
        }
    }

    // A point-anchored box's top or bottom edge, measured from the canvas's center.
    private static float EdgeY(RectTransform canvas, RectTransform box, bool top)
    {
        float center = (box.anchorMin.y - 0.5f) * canvas.rect.height + box.anchoredPosition.y +
                       box.rect.height * (0.5f - box.pivot.y);
        return center + (top ? 0.5f : -0.5f) * box.rect.height;
    }

    // -------------------- RESPONSE BOX --------------------

    private static void StyleResponse(RectTransform box)
    {
        var input = box.GetComponent<TMP_InputField>();
        if (input == null) return;

        TextPanel.AddPaper(box);

        // Room on the right for the scrollbar, so text never reflows when it appears.
        var viewport = input.textViewport;
        if (viewport != null)
        {
            viewport.offsetMin = new Vector2(12f, 9f);
            viewport.offsetMax = new Vector2(-(12f + TextPanel.ScrollbarGutter), -9f);

            // TMP's input field template pads its mask outward, which let a
            // half-cut line show in the box's margin once an answer scrolls.
            var mask = viewport.GetComponent<RectMask2D>();
            if (mask != null) mask.padding = Vector4.zero;
        }

        if (input.textComponent != null)
        {
            StyleBodyText(input.textComponent, Regular, ResponseFontSize);
            input.textComponent.lineSpacing = 2f;
        }
        if (input.placeholder is TMP_Text placeholder)
        {
            StyleBodyText(placeholder, Regular, ResponseFontSize);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = PlaceholderInk;
        }

        input.customCaretColor = true;
        input.caretColor = Ink;
        input.caretWidth = 2;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
        input.scrollSensitivity = 3f;
        // Clicking back into an answer puts the caret where you click. It used
        // to select the whole answer, so the next key typed replaced all of it.
        input.onFocusSelectAll = false;

        var scrollbar = TextPanel.MakeScrollbar(box, topInset: 9f, bottomInset: 9f);
        input.verticalScrollbar = scrollbar;
        box.gameObject.AddComponent<ScrollbarWhenOverflowing>().Init(input, scrollbar);
    }

    private static void StyleBodyText(TMP_Text text, TMP_FontAsset font, float size)
    {
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Normal;
        text.color = Ink;
        text.characterSpacing = 0f;
        text.wordSpacing = 0f;
        text.lineSpacing = 4f;
        text.paragraphSpacing = 0f;
        text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }
}
