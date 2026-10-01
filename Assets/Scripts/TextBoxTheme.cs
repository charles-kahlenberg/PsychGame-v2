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
//  - SavePromptPanel ("Do you want to save?"): a paper card on the dimmed
//    screen holding the question, the name box and the buttons.
// IntroductionScene's SpeechBubble gets the same panel as the scenario, sized
// to the NPC's line with its tail pointing at them. The other screens:
//  - RulesScene: Brainy's bubble is the panel with a "Brainy" name tag, its
//    tail toward Brainy; it holds the example too (RulesManager).
//  - GradingScene: the scenario, response and feedback scroll views become
//    fixed-size panels that scroll, and Brainy's score bubble is a small
//    panel with a name tag and a tail toward Brainy.
//  - ReviewScene: the saved responses are one tall panel that scrolls, under
//    a light title.
// GradingManager and ResponseReview colour their text for paper.
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

    private static readonly string[] SceneNames =
        { "GameScene", "IntroductionScene", "RulesScene", "GradingScene", "ReviewScene" };

    // Sizes in canvas units (the canvas is 450 tall).
    private const float ScenarioFontSize = 14f;
    private const float BubbleFontSize = 13f;
    private const float ResponseFontSize = 13f;
    public const float ScreenMargin = 12f;
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
            if (System.Array.IndexOf(SceneNames, scene.name) >= 0) Apply(scene);
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
            switch (scene.name)
            {
                case "IntroductionScene": StyleIntro(canvasRect); break;
                case "RulesScene": StyleRules(canvasRect); break;
                case "GradingScene": StyleGrading(canvasRect); break;
                case "ReviewScene": StyleReview(canvasRect); break;
                case "GameScene":
                    var response = canvas.transform.Find("ResponseInput") as RectTransform;

                    var overlay = canvas.transform.Find("HintOverlay")?.GetComponent<Image>();
                    if (overlay != null) overlay.color = OverlayTint;

                    StyleScenario(canvasRect, response);
                    StyleHintBubble(canvasRect, response, overlay);
                    if (response != null) StyleResponse(response);
                    StyleSavePrompt(canvasRect);
                    break;
            }
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

    // -------------------- INTRO --------------------

    private static void StyleIntro(RectTransform canvas)
    {
        var bubble = canvas.Find("SpeechBubble") as RectTransform;
        var text = bubble != null ? bubble.Find("DialogueText")?.GetComponent<TextMeshProUGUI>() : null;
        if (text == null) return;

        // From the bubble's old left edge to just short of the NPC's middle, so
        // the tail reaches over their head; never tall enough to cover their face.
        var npc = canvas.Find("NPC") as RectTransform;
        float rightAnchor = 0.8f, rightOffset = -40f, maxHeight = 120f;
        if (npc != null && npc.anchorMin == npc.anchorMax)
        {
            rightAnchor = npc.anchorMin.x;
            rightOffset = npc.anchoredPosition.x - npc.rect.width * 0.3f;
            float npcTopFromTop = canvas.rect.height / 2f - EdgeY(canvas, npc, top: true);
            maxHeight = Mathf.Max(60f, npcTopFromTop - ScreenMargin - 8f);
        }
        bubble.anchorMin = new Vector2(bubble.anchorMin.x, 1f);
        bubble.anchorMax = new Vector2(rightAnchor, 1f);
        bubble.pivot = new Vector2(0.5f, 1f);
        bubble.offsetMin = new Vector2(0f, -ScreenMargin - 60f);
        bubble.offsetMax = new Vector2(rightOffset, -ScreenMargin); // TextPanel sets the height

        StyleBodyText(text, Regular, ScenarioFontSize);
        var panel = TextPanel.Build(bubble, text, minHeight: 48f, maxHeight: maxHeight, nameTag: null);
        panel.AddTail(TextPanel.TailSide.Bottom, 0.9f);
    }

    // -------------------- RULES --------------------

    private static void StyleRules(RectTransform canvas)
    {
        var bubble = canvas.Find("BubbleBox") as RectTransform;
        var text = bubble != null ? bubble.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>() : null;
        if (text == null) return;

        // Like the intro: from the left margin to just short of Brainy's
        // middle, so the tail reaches toward them; never tall enough to cover them.
        var brainy = canvas.Find("Brainy") as RectTransform;
        float rightAnchor = 0.8f, rightOffset = -40f, maxHeight = 200f;
        if (brainy != null && brainy.anchorMin == brainy.anchorMax)
        {
            rightAnchor = brainy.anchorMin.x;
            rightOffset = brainy.anchoredPosition.x - brainy.rect.width * 0.3f;
            float brainyTopFromTop = canvas.rect.height / 2f - EdgeY(canvas, brainy, top: true);
            maxHeight = Mathf.Max(60f, brainyTopFromTop - ScreenMargin - 8f);
        }
        bubble.anchorMin = new Vector2(0f, 1f);
        bubble.anchorMax = new Vector2(rightAnchor, 1f);
        bubble.pivot = new Vector2(0.5f, 1f);
        bubble.offsetMin = new Vector2(2f * ScreenMargin, -ScreenMargin - 60f);
        bubble.offsetMax = new Vector2(rightOffset, -ScreenMargin); // TextPanel sets the height

        StyleBodyText(text, Regular, ScenarioFontSize);
        var panel = TextPanel.Build(bubble, text, minHeight: 52f, maxHeight: maxHeight, nameTag: "Brainy");
        panel.AddTail(TextPanel.TailSide.Bottom, 0.92f);
    }

    // -------------------- GRADING AND REVIEW --------------------

    private const float PanelGap = 12f; // between panels stacked down the screen

    private static void StyleGrading(RectTransform canvas)
    {
        var grading = Object.FindFirstObjectByType<GradingManager>();
        if (grading == null) return;

        FixedPanel(canvas, grading.scenarioText, top: ScreenMargin, bottom: PanelGap / 2f);
        FixedPanel(canvas, grading.userResponseText, top: PanelGap / 2f, bottom: PanelGap / 2f);
        FixedPanel(canvas, grading.aiResponseText, top: PanelGap / 2f, bottom: ScreenMargin);
        StyleScoreBubble(canvas, grading.scoreText);
    }

    private static void StyleReview(RectTransform canvas)
    {
        var review = Object.FindFirstObjectByType<ResponseReview>();
        if (review != null) FixedPanel(canvas, review.reviewText, top: PanelGap / 2f, bottom: ScreenMargin);

        // The "Review" title sits on the dark background, in the paper's colour.
        var title = canvas.Find("Text (TMP)")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.font = SemiBold;
            title.fontSharedMaterial = SemiBold.material;
            title.fontStyle = FontStyles.Normal;
            title.fontSize = 26f;
            title.color = Paper;
        }
    }

    // Replaces the scroll view holding `text` with a paper panel of the same
    // width (less the margins) and height (less `top` and `bottom`), which
    // scrolls once the text is longer. Height is fixed in canvas units, which
    // never change (the canvas matches the screen's height); width follows
    // the old view's anchors.
    private static void FixedPanel(RectTransform canvas, TMP_Text text, float top, float bottom)
    {
        var old = text != null ? text.GetComponentInParent<ScrollRect>(true)?.transform as RectTransform : null;
        if (old == null) return;

        Rect r = PixelButton.LocalRect(canvas, old);
        float height = r.height - top - bottom;
        float anchorY = canvas.rect.yMin + old.anchorMax.y * canvas.rect.height;

        var panel = (RectTransform)new GameObject(old.name + "Panel", typeof(RectTransform)).transform;
        panel.gameObject.layer = old.gameObject.layer;
        panel.SetParent(canvas, false);
        panel.SetSiblingIndex(old.GetSiblingIndex());
        panel.anchorMin = new Vector2(old.anchorMin.x, old.anchorMax.y);
        panel.anchorMax = new Vector2(old.anchorMax.x, old.anchorMax.y);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.sizeDelta = new Vector2(-2f * ScreenMargin, height);
        panel.anchoredPosition = new Vector2(0f, r.yMax - top - anchorY);

        StyleBodyText(text, Regular, BubbleFontSize);
        TextPanel.Build(panel, text, minHeight: height, maxHeight: height, nameTag: null);
        old.gameObject.SetActive(false);
    }

    // Brainy's score, beside Brainy: a small panel with their name tag, its
    // tail toward them, growing up from where the old bubble's bottom was.
    private static void StyleScoreBubble(RectTransform canvas, TMP_Text text)
    {
        var bubble = text != null ? text.transform.parent as RectTransform : null;
        if (bubble == null || bubble == canvas) return;

        float width = Mathf.Max(110f, PixelButton.LocalRect(canvas, bubble).width);
        float x = (bubble.anchorMin.x + bubble.anchorMax.x) / 2f;
        bubble.anchorMin = bubble.anchorMax = new Vector2(x, bubble.anchorMin.y);
        bubble.pivot = new Vector2(0.5f, 0f);
        bubble.anchoredPosition = Vector2.zero;
        bubble.sizeDelta = new Vector2(width, 40f);

        StyleBodyText(text, SemiBold, BubbleFontSize);
        text.alignment = TextAlignmentOptions.Top;
        var panel = TextPanel.Build(bubble, text, minHeight: 40f, maxHeight: 90f, nameTag: "Brainy");
        panel.AddTail(TextPanel.TailSide.Bottom, 0.85f);
    }

    // -------------------- SAVE PROMPT --------------------

    // A paper card in the middle of the dimmed screen, holding the question,
    // the name box and the Save / Skip buttons (PixelButton draws those).
    private static void StyleSavePrompt(RectTransform canvas)
    {
        var prompt = canvas.Find("SavePromptPanel") as RectTransform;
        if (prompt == null) return;

        var dim = prompt.GetComponent<Image>();
        if (dim != null)
        {
            dim.sprite = null;
            dim.color = OverlayTint;
        }

        var card = (RectTransform)new GameObject("Card", typeof(RectTransform)).transform;
        card.gameObject.layer = prompt.gameObject.layer;
        card.SetParent(prompt, false);
        card.SetAsFirstSibling();
        Center(card, new Vector2(0f, 52f), new Vector2(420f, 150f));
        TextPanel.AddPaper(card);

        var question = prompt.Find("Question")?.GetComponent<TMP_Text>();
        if (question != null)
        {
            Center(question.rectTransform, new Vector2(0f, 100f), new Vector2(380f, 30f));
            StyleBodyText(question, SemiBold, 18f);
            question.alignment = TextAlignmentOptions.Center;
        }

        var nameBox = prompt.Find("SaveNameInput") as RectTransform;
        var input = nameBox != null ? nameBox.GetComponent<TMP_InputField>() : null;
        if (input != null)
        {
            Center(nameBox, new Vector2(0f, 55f), new Vector2(340f, 36f));
            TextPanel.AddPaper(nameBox);
            if (input.textComponent != null) StyleBodyText(input.textComponent, Regular, ResponseFontSize);
            if (input.placeholder is TMP_Text placeholder)
            {
                StyleBodyText(placeholder, Regular, ResponseFontSize);
                placeholder.fontStyle = FontStyles.Italic;
                placeholder.color = PlaceholderInk;
            }
            foreach (var t in new[] { input.textComponent, input.placeholder as TMP_Text })
                if (t != null) t.alignment = TextAlignmentOptions.Left; // vertically centered in the box
            input.customCaretColor = true;
            input.caretColor = Ink;
            input.caretWidth = 2;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.3f);
        }

        foreach (var name in new[] { "Yes", "No" })
        {
            var button = prompt.Find(name) as RectTransform;
            if (button != null)
                Center(button, new Vector2(name == "Yes" ? -60f : 60f, 0f), button.sizeDelta);
        }
    }

    // Places a box by its middle, relative to its parent's middle.
    private static void Center(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
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
