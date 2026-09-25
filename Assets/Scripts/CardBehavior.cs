using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CardBehavior : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
    // Feature.FaceDownCards: cards sit face-down in the hand, like real cards.
    // Clicking one brings it to the center, where it flips over on its own to
    // reveal the term; clicking off flips it face-down again before it goes
    // back to the hand. The Help button becomes "Definition" and pops the
    // term's definition up in the hint bubble instead of asking the AI.

    private static bool FaceDownCards => TestGroups.IsEnabled(Feature.FaceDownCards);
    private const float DefinitionButtonWidth = 60f; // "Definition" doesn't fit the "Help" button's width
    private const float PresentDuration = 0.15f;     // hand -> center
    private const float ReturnDuration = 0.15f;      // center -> hand
    private const float FlipHalfDuration = 0.075f;   // edge-on, then the other side

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        originalRot = transform.localRotation;
        cardIndex = int.TryParse(name.Replace("Card", ""), out int n) ? n - 1 : 0;
        handSiblingIndex = transform.GetSiblingIndex();

        if (FaceDownCards)
        {
            SetUpDefinitionButton();
            TurnFaceDown();

            // Statics outlive the scene; start each visit with an empty line.
            waitingCards.Clear();
            cardHeadingHome = null;
        }
        else
        {
            ShowFront();
        }

        if (TestGroups.IsEnabled(Feature.CardTweening))
            DealIn(cardIndex * DealStagger);

        // Hide Help on start; wire click through UnityEvent OR here
        if (helpButton != null)
        {
            helpButton.gameObject.SetActive(false);
            // If you prefer auto-wiring (no UnityEvent), uncomment:
            // helpButton.onClick.RemoveListener(OnHelpButtonPressed);
            // helpButton.onClick.AddListener(OnHelpButtonPressed);
        }

        if (hintManager == null) hintManager = FindObjectOfType<HintManager>();
        if (hintManager == null) Debug.LogWarning("[CardBehavior] HintManager not found in scene.");
    }

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
            OnFaceDownCardClicked();
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CanHoverPop) LeanTween.scale(gameObject, originalScale * 1.1f, 0.15f).setEaseOutSine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (CanHoverPop) LeanTween.scale(gameObject, originalScale, 0.15f).setEaseInSine();
    }

    // Group 2 cards flip over at the center on the way home; a hover pop
    // there would shrink the card mid-flip.
    bool CanHoverPop => !isFocused && !(FaceDownCards && isReturning);

    public void ResetCard()
    {
        if (FaceDownCards)
        {
            LeanTween.cancel(gameObject);
            isReturning = true;
            cardHeadingHome = this;
            ClearFocus();

            if (isPresenting)
            {
                // Group 2, dismissed before it finished coming up: stop right
                // there and head home face-down from wherever it got to.
                isPresenting = false;
                isRevealed = false;
                ShowFaceDownState();
                ReturnToHand();
            }
            else if (isRevealed)
            {
                // Group 2: turn the card face-down where it is, then send it home.
                Flip(false, ReturnToHand);
            }
            else
            {
                ReturnToHand();
            }
            return;
        }

        isReturning = true;
        ReturnToHand();

        ClearFocus();
    }

    void ReturnToHand()
    {
        float duration = FaceDownCards ? ReturnDuration : 0.3f;
        LeanTween.moveLocal(gameObject, originalPos, duration).setEaseInOutCubic().setOnComplete(OnLandedInHand);
        LeanTween.scale(gameObject, originalScale, duration);
        LeanTween.rotateLocal(gameObject, originalRot.eulerAngles, duration);
    }

    void OnLandedInHand()
    {
        isReturning = false;
        if (!FaceDownCards) return;

        // Group 2: back in its place in the hand, then bring up the next card
        // that was clicked while this one had the center.
        LeaveCenterStage();

        while (waitingCards.Count > 0)
        {
            var next = waitingCards[0];
            waitingCards.RemoveAt(0);
            if (next != null && !next.isDealing)
            {
                next.Present();
                break;
            }
        }
    }

    // -------------------- GROUP 2 ONE CARD AT A TIME --------------------
    // Only one card is ever up at the center. Clicking another card while one
    // is presented (or still heading home) sends that one back to the hand
    // first; clicked cards then come up one at a time, in the order they were
    // clicked, each staying up until the next click. A card that's still on
    // its way up when the next click comes stops and heads straight home.
    // The presented card is moved later in the draw order, since UI draws in
    // hierarchy order and the hand's later cards would otherwise cover it.

    private static CardBehavior cardHeadingHome; // flipping back / returning, not yet landed
    private static readonly System.Collections.Generic.List<CardBehavior> waitingCards =
        new System.Collections.Generic.List<CardBehavior>(); // clicked, waiting for the center, oldest first
    private bool isPresenting;                   // on its way up / flipping face-up, not yet settled
    private int handSiblingIndex;                // draw order in the hand, restored on landing

    void OnFaceDownCardClicked()
    {
        if (isFocused || waitingCards.Contains(this)) return; // already up, or already in line

        var occupant = currentlyFocusedCard != null ? currentlyFocusedCard : cardHeadingHome;
        if (occupant == null || (occupant == this && waitingCards.Count == 0))
        {
            // Center's free, or this card is on its way home with nothing in
            // line behind it: bring it (straight back) up.
            Present();
            return;
        }

        if (occupant.isFocused) occupant.ResetCard();
        waitingCards.Add(this);
    }

    // Brings the card up to the center, where it flips face-up on arrival.
    void Present()
    {
        // Clicked again while still flipping back / heading home: drop that
        // and come straight back.
        LeanTween.cancel(gameObject);
        isReturning = false;
        if (cardHeadingHome == this) cardHeadingHome = null;
        waitingCards.Remove(this);

        transform.SetSiblingIndex(TopOfHandSiblingIndex()); // draw above the cards in the hand

        LeanTween.moveLocal(gameObject, Vector3.zero, PresentDuration).setEaseOutCubic();
        LeanTween.scale(gameObject, originalScale * 1.5f, PresentDuration).setEaseOutCubic();
        LeanTween.rotateLocal(gameObject, Vector3.zero, PresentDuration).setEaseOutCubic();

        isFocused = true;
        isPresenting = true;
        currentlyFocusedCard = this;

        LeanTween.delayedCall(gameObject, PresentDuration, OnReachedCenter);
    }

    // The cards share the Canvas with the hint popup, Brainy and the save
    // panel, so the presented card goes just above the topmost card in the
    // hand rather than to the very top (where it would cover the popup).
    int TopOfHandSiblingIndex()
    {
        int top = transform.GetSiblingIndex();
        foreach (Transform sibling in transform.parent)
        {
            if (sibling.GetComponent<CardBehavior>() != null)
                top = Mathf.Max(top, sibling.GetSiblingIndex());
        }
        return top;
    }

    // Puts the card back in its own spot in the hand's draw order.
    void LeaveCenterStage()
    {
        if (cardHeadingHome == this) cardHeadingHome = null;
        transform.SetSiblingIndex(handSiblingIndex);
    }

    // Group 2: the card has arrived at the center, so turn it face-up.
    void OnReachedCenter()
    {
        if (!isFocused) return;

        if (isRevealed)
        {
            ShowFaceDownState(); // already face-up (clicked back mid flip-back)
            isPresenting = false;
        }
        else
        {
            Flip(true, () => isPresenting = false);
        }
    }

    // Group 2: turns the card edge-on, swaps sides, then turns it back.
    void Flip(bool faceUp, System.Action onDone = null)
    {
        LeanTween.rotateY(gameObject, 90f, FlipHalfDuration).setOnComplete(() =>
        {
            isRevealed = faceUp;
            ShowFaceDownState();
            LeanTween.rotateY(gameObject, 0f, FlipHalfDuration).setOnComplete(() => onDone?.Invoke());
        });
    }

    void ClearFocus()
    {
        isFocused = false;
        isFlipped = false;
        currentlyFocusedCard = null;

        if (FaceDownCards)
        {
            ShowFaceDownState();
            return;
        }

        if (helpButton != null) helpButton.gameObject.SetActive(false);
        ShowFront();
    }

    // Group 2: puts the card face-down. GameManager calls this whenever a new
    // hand is shown, so fresh terms always start hidden.
    public void TurnFaceDown()
    {
        isRevealed = false;
        ShowFaceDownState();
    }

    // Flies the card up from below the screen into its place in the hand.
    public void DealIn(float delay)
    {
        isDealing = true;
        isReturning = false; // cancel() below would skip ResetCard's onComplete
        LeanTween.cancel(gameObject);

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
        if (isFocused) ClearFocus();
        transform.localScale = originalScale;

        if (FaceDownCards)
        {
            // A whole new hand is coming; nothing is waiting for the center.
            LeaveCenterStage();
            isPresenting = false;
            waitingCards.Clear();
        }

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
        transform.localRotation = originalRot * Quaternion.Euler(0f, 0f, degrees);
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
    private readonly System.Collections.Generic.List<GameObject> artLayers =
        new System.Collections.Generic.List<GameObject>();
    private Color? plainBodyColor; // the card's own color, from before any art was applied

    // Dresses the card in one area's art, replacing whatever it wore for the
    // last hand. Null puts the plain card back.
    public void ShowArt(CardArt.AreaArt art)
    {
        foreach (var layer in artLayers)
        {
            layer.SetActive(false);
            Destroy(layer);
        }
        artLayers.Clear();
        driftLayers.Clear();

        var body = GetComponent<Image>();
        if (body != null && plainBodyColor == null) plainBodyColor = body.color;

        int back = 0, front = 0;
        if (art != null)
        {
            // Take the shape of the card back so the art isn't squashed (keeps
            // the card's height, adjusts its width).
            var cardRect = (RectTransform)transform;
            var shape = FirstSprite(art.backLayers);
            if (shape != null)
            {
                float aspect = shape.rect.width / shape.rect.height;
                cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.y * aspect, cardRect.sizeDelta.y);
            }

            var cardSize = cardRect.rect.size;
            back = AddArtLayers(backFace, art.backLayers, cardSize);
            front = AddArtLayers(frontFace, art.frontLayers, cardSize);
        }

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

    int AddArtLayers(GameObject face, CardArt.Layer[] layers, Vector2 cardSize)
    {
        if (face == null || layers == null) return 0;

        int added = 0;
        foreach (var l in layers)
        {
            if (l == null || l.sprite == null) continue;

            var layer = new GameObject($"ArtLayer{added + 1}", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(face.transform, false);
            rect.SetSiblingIndex(added); // above earlier layers, below the face's text
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = cardSize;   // the face objects aren't card-sized, so size to the card

            var image = layer.GetComponent<Image>();
            image.sprite = l.sprite;
            image.raycastTarget = false; // clicks go to the card itself

            artLayers.Add(layer);
            if (l.drift) driftLayers.Add(rect);
            added++;
        }
        return added;
    }

    // A slow bob, sway and breathe, each on its own rhythm so the icon never
    // just moves in lockstep with the card underneath it.
    void ApplyDrift()
    {
        float t = Time.time * DriftSpeed + cardIndex * 2.1f;
        foreach (var rect in driftLayers)
        {
            if (!rect.gameObject.activeInHierarchy) continue;

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

    // Group 2. Face-down: the card back (its art, no text). Face-up: the term,
    // plus the Definition button while the card is focused.
    void ShowFaceDownState()
    {
        if (frontFace) frontFace.SetActive(isRevealed);
        if (backFace) backFace.SetActive(!isRevealed);
        if (helpButton != null) helpButton.gameObject.SetActive(isRevealed && isFocused);
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
