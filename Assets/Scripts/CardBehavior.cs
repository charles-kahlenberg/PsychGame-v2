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

    private const float FloatHeight = 5f;              // px up/down while idle
    private const float FloatSway = 1.2f;              // degrees of rotation while idle
    private const float FloatSpeed = 1.6f;

    private bool isDealing = false;   // deal/sweep in progress: ignore clicks, no float
    private bool isReturning = false; // ResetCard's return tween in progress: no float
    private float floatWeight = 0f;   // eases the float in so it never jumps
    private int cardIndex;

    // -------------------- GROUP 2 FACE-DOWN CARDS --------------------
    // Feature.FaceDownCards: cards are dealt blank, like the back of a real
    // card. Clicking a focused card flips it over to reveal the term, and it
    // stays face-up. The Help button becomes "Definition" and pops the term's
    // definition up in the hint bubble instead of asking the AI.

    private static bool FaceDownCards => TestGroups.IsEnabled(Feature.FaceDownCards);
    private const float DefinitionButtonWidth = 60f; // "Definition" doesn't fit the "Help" button's width

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        originalRot = transform.localRotation;
        cardIndex = int.TryParse(name.Replace("Card", ""), out int n) ? n - 1 : 0;

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

            if (FaceDownCards) ShowFaceDownState();
            else if (helpButton != null) helpButton.gameObject.SetActive(true);
        }
        else if (FaceDownCards)
        {
            if (isRevealed) return;

            // Flip the face-down card over to reveal its term. It stays
            // face-up from then on, like a real card.
            isRevealed = true;
            LeanTween.rotateY(gameObject, 90f, 0.15f).setOnComplete(() =>
            {
                ShowFaceDownState();
                LeanTween.rotateY(gameObject, 0f, 0.15f);
            });
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

        if (FaceDownCards)
        {
            ShowFaceDownState();
            return;
        }

        if (helpButton != null) helpButton.gameObject.SetActive(false);
        ShowFront();
    }

    // Group 2: puts the card back face-down (blank). GameManager calls this
    // whenever a new hand is shown, so fresh terms always start hidden.
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

    // Group 2. Face-down: just the blank card, no text. Face-up: the term,
    // plus the Definition button while the card is focused. The back face
    // (definition text) is never shown on the card; it opens in the popup.
    void ShowFaceDownState()
    {
        if (frontFace) frontFace.SetActive(isRevealed);
        if (backFace) backFace.SetActive(false);
        if (helpButton != null) helpButton.gameObject.SetActive(isRevealed && isFocused);
    }

    // Group 2: relabels the scene's Help button as Definition. Renaming the
    // object keeps the click logs accurate ("DefinitionButton", not "HelpButton").
    void SetUpDefinitionButton()
    {
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
