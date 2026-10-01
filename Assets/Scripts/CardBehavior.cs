using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardBehavior : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalPos;
    private Vector3 originalScale;
    private Quaternion originalRot;
    private bool isFocused = false;
    private bool isFlipped = false;  // group 1: definition side showing
    private bool isRevealed = false; // group 2: term showing (cards are dealt face-down)

    [Header("Faces")]
    public TextMeshProUGUI frontText;        // the term
    public TextMeshProUGUI backText;         // the definition (filled by GameManager)
    public GameObject frontFace;
    public GameObject backFace;

    [Header("Help Button")]
    public Button helpButton;                // Assign per card (the "Help" button under FrontFace)
    public TextMeshProUGUI scenarioText;     // Assign Canvas > Bubble Box > ScenarioText

    [Header("Hint UI (match Brain setup)")]
    public GameObject hintOverlay;           // Canvas > HintOverlay
    public RectTransform hintBubbleContainer;// Canvas > HintBubbleContainer (optional; just show it)
    public TMP_Text hintText;                // The TMP inside the overlay (e.g., HintTextBubble)
    public HintManager hintManager;          // Drag your HintManager here

    private static CardBehavior currentlyFocusedCard;

    // -------------------- GROUP 2 CARD TWEENING --------------------
    // Feature.CardTweening: cards deal in when the screen opens, sweep out and
    // back in on refresh, and gently float while sitting in the hand.

    public const float DealDuration = 0.45f;
    public const float DealStagger = 0.08f;           // delay between each card
    private static readonly Vector3 DealOffset = new Vector3(0f, -420f, 0f); // start below the screen
    private const float DealTilt = 12f;                // degrees of extra tilt while flying in

    private const float FloatHeight = 3.5f;            // px up/down while idle
    private const float FloatSway = 0.84f;             // degrees of rotation while idle
    private const float FloatSpeed = 1.6f;

    private bool isDealing = false;   // deal/sweep in progress: ignore clicks, no float
    private bool isReturning = false; // ResetCard's return tween in progress: no float
    private float floatWeight = 0f;   // eases the float in so it never jumps
    private int cardIndex;

    // -------------------- GROUP 2 FACE-DOWN CARDS --------------------
    // Feature.FaceDownCards: cards are dealt face-down, like real cards.
    // Clicking one flips it over right there in the hand to reveal the term,
    // and it stays face-up until the next hand. The Help button becomes
    // "Definition", shows on every face-up card, and pops the term's
    // definition up in the hint bubble instead of asking the AI.

    private static bool FaceDownCards => TestGroups.IsEnabled(Feature.FaceDownCards);
    private const float DefinitionButtonWidth = 60f; // "Definition" doesn't fit the "Help" button's width
    private const float FlipHalfDuration = 0.15f;    // edge-on, then the other side

    private bool isFlippingInHand = false;
    private float tilt;       // current extra tilt around Z (deal, sweep, idle sway)
    private float flipAngle;  // current turn around the card's own Y while flipping in the hand

    void Awake()
    {
        if (TestGroups.IsEnabled(Feature.CardTweening))
        {
            GiveOwnCanvas();

            // The card's Button uses Animation transitions, but every clip in
            // its controller is empty: the Animator does nothing visible yet
            // still runs every frame. LeanTween drives all of group 2's motion.
            var animator = GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
        }
    }

    // Group 2's cards move every frame (float, icon drift). On the scene's
    // one big Canvas, each tiny move made Unity rebatch everything on it
    // (text, response box, background) on the main thread, every frame. A
    // nested Canvas per card keeps that work to the card itself. It also
    // turns off the scene's Pixel Perfect for the card: snapping a few-pixel
    // bob to whole pixels made the cards step instead of glide.
    void GiveOwnCanvas()
    {
        var parent = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        if (parent == null || GetComponent<Canvas>() != null) return;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.overridePixelPerfect = true;
        canvas.pixelPerfect = false;
        canvas.additionalShaderChannels = parent.additionalShaderChannels; // TextMeshPro needs these

        // A nested Canvas's graphics are only clickable through its own raycaster.
        gameObject.AddComponent<GraphicRaycaster>();
    }

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        originalRot = transform.localRotation;
        cardIndex = int.TryParse(name.Replace("Card", ""), out int n) ? n - 1 : 0;
        FitTermInsideCard();

        if (FaceDownCards)
        {
            SetUpDefinitionButton();
            TurnFaceDown();
        }
        else
        {
            ShowFront();
        }

        if (TestGroups.IsEnabled(Feature.CardTweening))
            DealIn(cardIndex * DealStagger);

        // Hide Help on start; wire click through UnityEvent OR here. Group 2's
        // Definition button stays active and shows and hides with the front face.
        if (helpButton != null && !FaceDownCards)
        {
            helpButton.gameObject.SetActive(false);
            // If you prefer auto-wiring (no UnityEvent), uncomment:
            // helpButton.onClick.RemoveListener(OnHelpButtonPressed);
            // helpButton.onClick.AddListener(OnHelpButtonPressed);
        }

        if (hintManager == null) hintManager = FindObjectOfType<HintManager>();
        if (hintManager == null) Debug.LogWarning("[CardBehavior] HintManager not found in scene.");
    }

    // The term's box spanned the whole card with no padding and never shrank,
    // so a long word (e.g. "Temperament") ran past the card's edges, where the
    // next card in the hand covered it. It now keeps clear of the edges, and
    // terms too long for that shrink until they fit.
    void FitTermInsideCard()
    {
        if (frontText == null) return;
        var rt = frontText.rectTransform;
        rt.offsetMin = new Vector2(TermSidePadding, rt.offsetMin.y);
        rt.offsetMax = new Vector2(-TermSidePadding, rt.offsetMax.y);
        frontText.fontSizeMax = frontText.fontSize;
        frontText.fontSizeMin = TermMinFontSize;
        frontText.enableAutoSizing = true;
    }

    private const float TermSidePadding = 10f;
    private const float TermMinFontSize = 8f;

    void Update()
    {
        if (TestGroups.IsEnabled(Feature.CardTweening))
            ApplyIdleFloat();

        if (driftLayers.Count > 0)
            ApplyDrift();

        // Click-off to reset
        if (isFocused && Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                ResetCard();
            }
            else
            {
                var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, results);

                bool clickedOnThisCard = false;
                foreach (var r in results)
                {
                    if (r.gameObject == gameObject || r.gameObject.transform.IsChildOf(transform))
                    {
                        clickedOnThisCard = true;
                        break;
                    }
                }
                if (!clickedOnThisCard) ResetCard();
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDealing) return;

        if (FaceDownCards)
        {
            // Group 2: face-down cards flip over in the hand; face-up ones
            // stay put (their Definition button handles the rest).
            if (!isRevealed && !isFlippingInHand) FlipInHand();
            return;
        }

        // Only one focused at a time
        if (currentlyFocusedCard != null && currentlyFocusedCard != this)
            currentlyFocusedCard.ResetCard();

        if (!isFocused)
        {
            // Focus animation
            LeanTween.moveLocal(gameObject, Vector3.zero, 0.3f).setEaseOutCubic();
            LeanTween.scale(gameObject, originalScale * 1.5f, 0.3f).setEaseOutCubic();
            LeanTween.rotateLocal(gameObject, Vector3.zero, 0.3f).setEaseOutCubic();

            isFocused = true;
            currentlyFocusedCard = this;

            if (helpButton != null) helpButton.gameObject.SetActive(true);
        }
        else if (!isFlipped)
        {
            LeanTween.rotateY(gameObject, 90f, 0.15f).setOnComplete(() =>
            {
                ShowBack();
                LeanTween.rotateY(gameObject, 0f, 0.15f);
            });
            isFlipped = true;
        }
        else
        {
            LeanTween.rotateY(gameObject, 90f, 0.15f).setOnComplete(() =>
            {
                ShowFront();
                LeanTween.rotateY(gameObject, 0f, 0.15f);
            });
            isFlipped = false;
        }
    }

    // Group 2: a face-down card starts flipping as soon as it's pressed,
    // rather than on release, so the card answers the click right away.
    // OnPointerClick then sees it's already flipping and does nothing.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (FaceDownCards && !isDealing && !isRevealed && !isFlippingInHand) FlipInHand();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isFocused) LeanTween.scale(gameObject, originalScale * 1.1f, 0.15f).setEaseOutSine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isFocused) LeanTween.scale(gameObject, originalScale, 0.15f).setEaseInSine();
    }

    public void ResetCard()
    {
        isReturning = true;
        LeanTween.moveLocal(gameObject, originalPos, 0.3f).setEaseInOutCubic().setOnComplete(() => isReturning = false);
        LeanTween.scale(gameObject, originalScale, 0.3f);
        LeanTween.rotateLocal(gameObject, originalRot.eulerAngles, 0.3f);

        ClearFocus();
    }

    void ClearFocus()
    {
        isFocused = false;
        isFlipped = false;
        currentlyFocusedCard = null;

        if (helpButton != null) helpButton.gameObject.SetActive(false);
        ShowFront();
    }

    // Group 2: turns the card edge-on where it sits in the hand, swaps it to
    // the term side, then turns it back. Driven through flipAngle (rather
    // than rotateY) so it combines with the hand's tilt and idle sway.
    //
    // The UI is flat, so turning around Y only narrows the card to cos(angle)
    // of its width. Tweening that width instead of the angle makes the card
    // visibly start turning the instant it's clicked (easing the angle left it
    // looking still for the first few frames), then the term side settles in.
    void FlipInHand()
    {
        isFlippingInHand = true;
        LeanTween.value(gameObject, 1f, 0f, FlipHalfDuration)
            .setOnUpdate(SetFlipWidth)
            .setOnComplete(() =>
            {
                isRevealed = true;
                ShowFaceDownState();
                LeanTween.value(gameObject, 0f, 1f, FlipHalfDuration).setEaseOutQuad()
                    .setOnUpdate(SetFlipWidth)
                    .setOnComplete(() => isFlippingInHand = false);
            });
    }

    // Width as a fraction of the card's full width: 1 flat on, 0 edge-on.
    void SetFlipWidth(float width)
    {
        flipAngle = Mathf.Acos(Mathf.Clamp01(width)) * Mathf.Rad2Deg;
        ApplyRotation();
    }

    // Stops a flip partway (a new hand is being dealt) and squares the card up.
    void StopFlipInHand()
    {
        isFlippingInHand = false;
        flipAngle = 0f;
    }

    // Group 2: puts the card face-down. GameManager calls this whenever a new
    // hand is shown, so fresh terms always start hidden.
    public void TurnFaceDown()
    {
        if (isFlippingInHand)
        {
            // Mid-flip when the new hand arrived: stop it so it can't finish
            // by turning up the new term.
            LeanTween.cancel(gameObject);
            StopFlipInHand();
            ApplyRotation();
        }

        isRevealed = false;
        ShowFaceDownState();
    }

    // Flies the card up from below the screen into its place in the hand.
    public void DealIn(float delay)
    {
        isDealing = true;
        isReturning = false; // cancel() below would skip ResetCard's onComplete
        LeanTween.cancel(gameObject);
        StopFlipInHand();

        transform.localPosition = originalPos + DealOffset;
        transform.localScale = originalScale;
        SetTilt(DealTilt);

        LeanTween.moveLocal(gameObject, originalPos, DealDuration).setDelay(delay).setEaseOutBack();
        LeanTween.value(gameObject, 1f, 0f, DealDuration).setDelay(delay).setEaseOutCubic()
            .setOnUpdate(t => SetTilt(DealTilt * t))
            .setOnComplete(() => isDealing = false);
    }

    // Drops the card out of the hand, below the screen. It stays there until DealIn.
    public void SweepOut(float delay)
    {
        isDealing = true;
        isReturning = false;
        LeanTween.cancel(gameObject);
        StopFlipInHand();
        if (isFocused) ClearFocus();
        transform.localScale = originalScale;

        LeanTween.moveLocal(gameObject, originalPos + DealOffset, DealDuration * 0.7f).setDelay(delay).setEaseInBack();
        LeanTween.value(gameObject, 0f, -1f, DealDuration * 0.7f).setDelay(delay).setEaseInCubic()
            .setOnUpdate(t => SetTilt(DealTilt * t));
    }

    // How long a whole hand takes to sweep out / deal in with the stagger.
    public static float SweepOutTime(int cardCount) => DealDuration * 0.7f + DealStagger * (cardCount - 1);
    public static float DealInTime(int cardCount) => DealDuration + DealStagger * (cardCount - 1);

    // Rotation relative to where the card rests in the hand, around Z only.
    // Driven through LeanTween.value rather than rotateLocal so it never
    // takes the long way around when the angle wraps past 0/360.
    void SetTilt(float degrees)
    {
        tilt = degrees;
        ApplyRotation();
    }

    // The resting angle, plus the tilt, plus any in-hand flip (group 2).
    void ApplyRotation()
    {
        transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(0f, flipAngle, 0f);
    }

    // Gentle bob and sway while the card is just sitting in the hand. Each
    // card is out of phase with the others so the hand looks alive.
    void ApplyIdleFloat()
    {
        bool idle = !isFocused && !isDealing && !isReturning;
        if (!idle)
        {
            floatWeight = 0f;
            return;
        }

        floatWeight = Mathf.MoveTowards(floatWeight, 1f, Time.deltaTime / 0.6f);
        float phase = cardIndex * 1.3f;
        float t = Time.time * FloatSpeed + phase;

        transform.localPosition = originalPos + new Vector3(0f, Mathf.Sin(t) * FloatHeight * floatWeight, 0f);
        SetTilt(Mathf.Sin(t * 0.8f + phase) * FloatSway * floatWeight);
    }

    // -------------------- GROUP 2 CARD ART --------------------
    // Feature.NewCardArt: GameManager hands each card the art for its term's
    // area of psychology whenever a hand is shown. The layers are stacked onto
    // each side of the card, bottom layer first, underneath the text and
    // buttons already there. Each layer fills the whole card; layers marked
    // "drift" (the back's icon) gently move on their own on top of the card's
    // own float.

    private const float DriftBob = 2.5f;     // px up/down
    private const float DriftSway = 2.5f;    // degrees of rotation
    private const float DriftPulse = 0.025f; // fraction of size it breathes in/out
    private const float DriftSpeed = 1.1f;   // deliberately out of step with the card's float

    private readonly System.Collections.Generic.List<RectTransform> driftLayers =
        new System.Collections.Generic.List<RectTransform>();
    // Each face's art layer images, reused from hand to hand: re-creating
    // them on every refresh made a slow frame right as the new hand dealt in.
    private readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<Image>> artLayers =
        new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<Image>>();
    private Color? plainBodyColor; // the card's own color, from before any art was applied

    // Dresses the card in one area's art, replacing whatever it wore for the
    // last hand. Null puts the plain card back.
    public void ShowArt(CardArt.AreaArt art)
    {
        driftLayers.Clear();

        var body = GetComponent<Image>();
        if (body != null && plainBodyColor == null) plainBodyColor = body.color;

        Vector2 cardSize = Vector2.zero;
        if (art != null)
        {
            // Take the shape of the card back so the art isn't squashed (keeps
            // the card's height, adjusts its width).
            var cardRect = (RectTransform)transform;
            var shape = FirstSprite(art.backLayers);
            if (shape != null)
            {
                float aspect = shape.rect.width / shape.rect.height;
                var size = new Vector2(cardRect.sizeDelta.y * aspect, cardRect.sizeDelta.y);
                if (cardRect.sizeDelta != size) cardRect.sizeDelta = size;
            }
            cardSize = cardRect.rect.size;
        }

        int back = SetArtLayers(backFace, art?.backLayers, cardSize);
        int front = SetArtLayers(frontFace, art?.frontLayers, cardSize);

        // With art on both sides, the art is the card: the plain card image
        // goes invisible but still catches clicks (it's the Button's graphic).
        if (body != null) body.color = back > 0 && front > 0 ? Color.clear : plainBodyColor.Value;
    }

    static Sprite FirstSprite(CardArt.Layer[] layers)
    {
        if (layers == null) return null;
        foreach (var l in layers)
            if (l != null && l.sprite != null) return l.sprite;
        return null;
    }

    // Shows the given layers on a face, bottom first, underneath the face's
    // text; any of the face's layers left over from the last hand are hidden.
    int SetArtLayers(GameObject face, CardArt.Layer[] layers, Vector2 cardSize)
    {
        if (face == null) return 0;
        if (!artLayers.TryGetValue(face, out var pool))
            artLayers[face] = pool = new System.Collections.Generic.List<Image>();

        int used = 0;
        if (layers != null)
        {
            foreach (var l in layers)
            {
                if (l == null || l.sprite == null) continue;

                Image image = used < pool.Count ? pool[used] : NewArtLayer(face, pool);
                var rect = image.rectTransform;
                if (rect.GetSiblingIndex() != used) rect.SetSiblingIndex(used); // above earlier layers, below the face's text
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                if (rect.sizeDelta != cardSize) rect.sizeDelta = cardSize; // the face objects aren't card-sized, so size to the card

                if (image.sprite != l.sprite) image.sprite = l.sprite;
                if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);

                if (l.drift) driftLayers.Add(rect);
                used++;
            }
        }

        for (int i = used; i < pool.Count; i++)
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);

        return used;
    }

    static Image NewArtLayer(GameObject face, System.Collections.Generic.List<Image> pool)
    {
        var layer = new GameObject($"ArtLayer{pool.Count + 1}", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)layer.transform;
        rect.SetParent(face.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        var image = layer.GetComponent<Image>();
        image.raycastTarget = false; // clicks go to the card itself
        pool.Add(image);
        return image;
    }

    // A slow bob, sway and breathe, each on its own rhythm so the icon never
    // just moves in lockstep with the card underneath it.
    void ApplyDrift()
    {
        float t = Time.time * DriftSpeed + cardIndex * 2.1f;
        foreach (var rect in driftLayers)
        {
            if (!rect.gameObject.activeInHierarchy || !FaceShowing(rect.parent)) continue;

            rect.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 1.7f) * DriftBob);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f + 0.8f) * DriftSway);
            rect.localScale = Vector3.one * (1f + Mathf.Sin(t * 2.3f + 1.9f) * DriftPulse);
        }
    }

    void ShowFront()
    {
        if (frontFace) frontFace.SetActive(true);
        if (backFace) backFace.SetActive(false);
    }

    void ShowBack()
    {
        if (frontFace) frontFace.SetActive(false);
        if (backFace) backFace.SetActive(true);
    }

    // Group 2. Face-down: the card back (its art, no text). Face-up: the term
    // and its Definition button.
    //
    // Both faces stay active and are shown and hidden through CanvasGroups.
    // Re-activating a face mid-flip made Unity rebuild all its graphics and
    // text in that frame, and the flip's second half stuttered on it.
    private CanvasGroup frontGroup, backGroup;

    void ShowFaceDownState()
    {
        if (frontGroup == null && frontFace) frontGroup = FaceGroup(frontFace);
        if (backGroup == null && backFace) backGroup = FaceGroup(backFace);

        SetFaceShown(frontGroup, isRevealed);
        SetFaceShown(backGroup, !isRevealed);
    }

    static CanvasGroup FaceGroup(GameObject face)
    {
        face.SetActive(true);
        var group = face.GetComponent<CanvasGroup>();
        return group != null ? group : face.AddComponent<CanvasGroup>();
    }

    static void SetFaceShown(CanvasGroup group, bool shown)
    {
        if (group == null) return;
        group.alpha = shown ? 1f : 0f;
        group.blocksRaycasts = shown; // a hidden face's Definition button can't be clicked
        group.interactable = shown;
    }

    // False for a group 2 face hidden by its CanvasGroup.
    bool FaceShowing(Transform face)
    {
        if (frontGroup != null && face == frontGroup.transform) return frontGroup.alpha > 0f;
        if (backGroup != null && face == backGroup.transform) return backGroup.alpha > 0f;
        return true;
    }

    // Group 2: relabels the scene's Help button as Definition. Renaming the
    // object keeps the click logs accurate ("DefinitionButton", not "HelpButton").
    // The definition text moves off the card back and into the popup.
    void SetUpDefinitionButton()
    {
        if (backText != null) backText.gameObject.SetActive(false);
        if (helpButton == null) return;

        helpButton.name = "DefinitionButton";
        var label = helpButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Definition";

        var rect = (RectTransform)helpButton.transform;
        rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, DefinitionButtonWidth), rect.sizeDelta.y);
    }

    // PUBLIC so you can wire it in Button OnClick exactly like BrainHint.OnBrainClicked.
    // Group 1 asks the AI for help with the concept; group 2 shows the definition.
    public void OnHelpButtonPressed()
    {
        if (FaceDownCards) ShowDefinition();
        else RequestAiConceptHelp();
    }

    // Group 2: pops the term's definition up in the same bubble Brainy's hints use.
    void ShowDefinition()
    {
        if (hintOverlay == null || hintText == null)
        {
            Debug.LogWarning("[CardBehavior] Assign Hint Overlay and Hint Text fields on the card.");
            return;
        }

        string term = frontText != null ? frontText.text.Trim() : "";
        string definition = backText != null ? backText.text.Trim() : "";

        OpenHintBubble();
        hintText.text = string.IsNullOrEmpty(definition)
            ? "No definition available."
            : $"<b>{term}</b>\n\n{definition}";

        // Group 2: Brainy steps up to the bubble its tail points at.
        if (TextBoxTheme.Enabled)
        {
            var brain = FindFirstObjectByType<BrainBehavior>();
            if (brain != null) brain.StepUpToBubble();
        }
    }

    void OpenHintBubble()
    {
        // 1) Open overlay + bubble
        hintOverlay.SetActive(true);
        if (hintBubbleContainer != null) hintBubbleContainer.gameObject.SetActive(true);

        // 2) Ensure bubble sits IN FRONT of overlay
        //    Case 1: Same Canvas -> use sibling order
        hintOverlay.transform.SetAsLastSibling();          // put overlay near top
        if (hintBubbleContainer != null)
            hintBubbleContainer.SetAsLastSibling();        // then bubble on very top

        //    Case 2: Different canvases -> use sorting order
        var overlayCanvas = hintOverlay.GetComponentInParent<Canvas>();
        if (overlayCanvas != null)
        {
            overlayCanvas.overrideSorting = true;
            if (overlayCanvas.sortingOrder < 50) overlayCanvas.sortingOrder = 50;
        }
        var bubbleCanvas = hintBubbleContainer != null ? hintBubbleContainer.GetComponentInParent<Canvas>() : null;
        if (bubbleCanvas != null)
        {
            bubbleCanvas.overrideSorting = true;
            if (bubbleCanvas.sortingOrder <= (overlayCanvas != null ? overlayCanvas.sortingOrder : 50))
                bubbleCanvas.sortingOrder = (overlayCanvas != null ? overlayCanvas.sortingOrder + 1 : 51);
        }
    }

    // Group 1: asks the AI how this card's concept applies to the current scenario.
    public void RequestAiConceptHelp()
    {
        if (hintManager == null)
        {
            hintManager = FindObjectOfType<HintManager>();
            if (hintManager == null)
            {
                Debug.LogWarning("[CardBehavior] No HintManager assigned/found.");
                return;
            }
        }

        if (hintOverlay == null || hintText == null)
        {
            Debug.LogWarning("[CardBehavior] Assign Hint Overlay and Hint Text fields on the card.");
            return;
        }

        OpenHintBubble();

        // 3) Point HintManager at the SAME TMP the bubble uses, so the AI fills it
        hintManager.hintText = hintText;

        // 4) Show immediate feedback
        hintText.text = "Please wait...";

        // 5) Gather concept & scenario
        string concept = frontText != null ? frontText.text.Trim() : "";
        string scenario = scenarioText != null ? scenarioText.text.Trim() : PlayerPrefs.GetString("LastScenario", "");

        if (string.IsNullOrWhiteSpace(concept))
        {
            Debug.LogWarning("[CardBehavior] Concept (frontText) is empty.");
            return;
        }
        if (string.IsNullOrWhiteSpace(scenario))
        {
            Debug.LogWarning("[CardBehavior] Scenario text is empty.");
            return;
        }

        // 6) Ask AI using the concept-aware prompt
        hintManager.RequestConceptHelp(scenario, concept);
    }

}
