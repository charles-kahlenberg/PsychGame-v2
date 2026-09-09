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
    private bool isFlipped = false;

    [Header("Faces")]
    public TextMeshProUGUI frontText;
    public TextMeshProUGUI backText;
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

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        originalRot = transform.localRotation;

        ShowFront();

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
        if (!isFocused) LeanTween.scale(gameObject, originalScale * 1.1f, 0.15f).setEaseOutSine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isFocused) LeanTween.scale(gameObject, originalScale, 0.15f).setEaseInSine();
    }

    public void ResetCard()
    {
        LeanTween.moveLocal(gameObject, originalPos, 0.3f).setEaseInOutCubic();
        LeanTween.scale(gameObject, originalScale, 0.3f);
        LeanTween.rotateLocal(gameObject, originalRot.eulerAngles, 0.3f);

        isFocused = false;
        isFlipped = false;
        currentlyFocusedCard = null;

        if (helpButton != null) helpButton.gameObject.SetActive(false);
        ShowFront();
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

    // PUBLIC so you can wire it in Button OnClick exactly like BrainHint.OnBrainClicked
    public void OnHelpButtonPressed()
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
