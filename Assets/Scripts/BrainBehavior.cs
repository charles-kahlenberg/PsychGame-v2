using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class BrainBehavior : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 originalPos;
    private Vector3 originalScale;
    private bool isActive = false;
    private bool isHovered = false;

    // Group 2 attention cue (Feature.BrainyAttentionCue): while idle, Brainy
    // hops every AttentionCueInterval seconds so players notice it.
    private const float AttentionCueInterval = 6f;
    private const float AttentionCueHopHeight = 12f;

    // Group 2 resting spot (Feature.BrainyBesideResponseBox): this far to the
    // left of the response box, vertically centered on it.
    private const float ResponseBoxGap = 12f;

    // Group 2 (Feature.JarBrainy): here the jar Brainy (JarBrainy) is drawn
    // a little bigger than the old one, and a touch wider than the art, and
    // gently bobs and sways like the cards' idle float whenever nothing else
    // is moving it.
    private static readonly Vector2 JarSize = new Vector2(1.3f, 1.15f); // x and y scale of the scene's size
    private const float IdleBob = 1.5f;   // px up/down
    private const float IdleSway = 1.2f;  // degrees of rotation
    private const float IdleSpeed = 1.3f;
    private bool jarArt;
    private float idleWeight; // eases the float in and out so it never jumps

    // Before TextBoxTheme measures Brainy (on scene load) to place the hint
    // bubble, and before Start rests it beside the response box.
    void Awake()
    {
        if (!TestGroups.IsEnabled(Feature.JarBrainy) || !JarBrainy.Apply(gameObject, stretch: true)) return;

        jarArt = true;
        var rt = (RectTransform)transform;
        rt.sizeDelta = Vector2.Scale(rt.sizeDelta, JarSize);
    }

    public Vector3 focusPosition = new Vector3(500f, 0f, 0f);
    public GameObject hintOverlay;
    public GameObject hintText;
    public TMP_Text scenarioText;
    public HintManager hintManager;

    void Start()
    {
        if (TestGroups.IsEnabled(Feature.BrainyBesideResponseBox))
            MoveBesideResponseBox();

        originalPos = transform.localPosition;
        originalScale = transform.localScale;

        if (hintOverlay != null) hintOverlay.SetActive(false);
        if (hintText != null) hintText.SetActive(false);

        if (TestGroups.IsEnabled(Feature.BrainyAttentionCue))
            StartCoroutine(AttentionCueLoop());
    }

    // Measured from the box itself, so Brainy follows it if the layout changes
    // and stays beside it on any screen shape. Only the resting spot moves:
    // clicking still flies Brainy to the same focusPosition by the hint bubble.
    void MoveBesideResponseBox()
    {
        var box = GameObject.Find("ResponseInput")?.transform as RectTransform;
        if (box == null || box.anchorMin != box.anchorMax)
        {
            Debug.LogWarning("[BrainBehavior] ResponseInput not found (or not point-anchored); leaving Brainy where it is.");
            return;
        }

        var rt = (RectTransform)transform;
        rt.anchorMin = box.anchorMin;
        rt.anchorMax = box.anchorMax;

        float boxLeft = box.anchoredPosition.x - box.rect.width * box.pivot.x;
        float boxMiddle = box.anchoredPosition.y + (0.5f - box.pivot.y) * box.rect.height;
        rt.anchoredPosition = new Vector2(
            boxLeft - ResponseBoxGap - rt.rect.width * (1f - rt.pivot.x),
            boxMiddle - (0.5f - rt.pivot.y) * rt.rect.height);
    }

    // Off while a tween has Brainy (a hop, hover, the click to the bubble,
    // the synopsis slide-in) or the bubble is open; it eases back from
    // wherever that left Brainy.
    void ApplyIdleFloat()
    {
        bool idle = !isActive && !isHovered && !LeanTween.isTweening(gameObject);
        idleWeight = Mathf.MoveTowards(idleWeight, idle ? 1f : 0f, Time.deltaTime / 0.6f);

        float t = Time.time * IdleSpeed;
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f + 1.1f) * IdleSway * idleWeight);
        if (!idle) return;

        Vector3 target = originalPos + new Vector3(0f, Mathf.Sin(t) * IdleBob * idleWeight, 0f);
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    IEnumerator AttentionCueLoop()
    {
        var wait = new WaitForSeconds(AttentionCueInterval);
        while (true)
        {
            yield return wait;
            if (isActive || isHovered) continue;

            // Two quick hops with a little grow, then back to rest.
            LeanTween.moveLocalY(gameObject, originalPos.y + AttentionCueHopHeight, 0.15f).setEaseOutQuad().setLoopPingPong(2);
            LeanTween.scale(gameObject, originalScale * 1.08f, 0.15f).setEaseOutQuad().setLoopPingPong(2);
        }
    }

    // Stops a hop in progress so it can't fight the hover/click tweens.
    void CancelAttentionCue()
    {
        if (!TestGroups.IsEnabled(Feature.BrainyAttentionCue)) return;

        LeanTween.cancel(gameObject);
        transform.localPosition = originalPos;
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isActive) return;

        isActive = true;
        awaitingBubble = true;
        activatedFrame = Time.frameCount;

        CancelAttentionCue();
        LeanTween.moveLocal(gameObject, focusPosition, 0.4f).setEaseOutExpo();
        LeanTween.scale(gameObject, originalScale * 1.3f, 0.4f).setEaseOutBack();

        Invoke(nameof(ShowHint), 0.5f);
    }

    // Group 2 (Feature.ThemedTextBoxes): whenever Brainy's bubble is showing
    // (a hint, or a card's definition), Brainy steps forward to its tail the
    // same way it does when clicked for a hint, without asking for one, and
    // steps back when it closes.
    public void StepUpToBubble()
    {
        if (isActive) return;

        isActive = true;
        activatedFrame = Time.frameCount;
        CancelAttentionCue();
        LeanTween.moveLocal(gameObject, focusPosition, 0.4f).setEaseOutExpo();
        LeanTween.scale(gameObject, originalScale * 1.3f, 0.4f).setEaseOutBack();
    }

    void ShowHint()
    {
        awaitingBubble = false;
        if (hintOverlay != null) hintOverlay.SetActive(true);
        if (hintText != null) hintText.SetActive(true);

        string scenario = scenarioText.text;

        List<string> currentCards = new List<string>();

        // Loop through Card1–Card5 directly by name
        for (int i = 1; i <= 5; i++)
        {
            string cardName = "Card" + i;
            GameObject cardObj = GameObject.Find(cardName);

            if (cardObj != null)
            {
                Transform cardTextTransform = cardObj.transform.Find("FrontFace/CardText" + i);
                if (cardTextTransform != null)
                {
                    TMP_Text cardText = cardTextTransform.GetComponent<TMP_Text>();
                    if (cardText != null && !string.IsNullOrWhiteSpace(cardText.text))
                    {
                        currentCards.Add(cardText.text.Trim());
                    }
                }
                else
                {
                    Debug.LogWarning($"CardText{i} not found under {cardName}");
                }
            }
            else
            {
                Debug.LogWarning($"Card object '{cardName}' not found in scene.");
            }
        }

        Debug.Log("Sending cards to hint generator: " + string.Join(", ", currentCards));
        hintManager.RequestHint(scenario, currentCards);
    }

    private bool awaitingBubble; // clicked, but the bubble opens a moment later
    private int activatedFrame = -1; // the click that activated Brainy isn't a click away

    void Update()
    {
        if (jarArt) ApplyIdleFloat();

        // However the bubble opened (this script, BrainHint, or a card's
        // definition), Brainy goes to it.
        if (TextBoxTheme.Enabled && !isActive && hintText != null && hintText.activeInHierarchy)
            StepUpToBubble();

        if (TextBoxTheme.Enabled && isActive && !awaitingBubble && hintText != null)
        {
            // However the bubble was closed (a click outside it, Esc), Brainy
            // steps back with it. While it's open Brainy stays above the dimmed
            // overlay, beside the bubble's tail.
            if (!hintText.activeInHierarchy)
            {
                ResetBrain();
                return;
            }
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1)
                transform.SetAsLastSibling();
        }

        if (isActive && Input.GetMouseButtonDown(0) && Time.frameCount != activatedFrame)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            bool clickedOnSelfOrHint = false;

            foreach (var result in raycastResults)
            {
                if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform) ||
                    result.gameObject == hintOverlay || (hintOverlay != null && result.gameObject.transform.IsChildOf(hintOverlay.transform)) ||
                    result.gameObject == hintText || (hintText != null && result.gameObject.transform.IsChildOf(hintText.transform)))
                {
                    clickedOnSelfOrHint = true;
                    break;
                }
            }

            if (!clickedOnSelfOrHint)
            {
                ResetBrain();
            }
        }
    }

    void ResetBrain()
    {
        LeanTween.moveLocal(gameObject, originalPos, 0.4f).setEaseInOutExpo();
        LeanTween.scale(gameObject, originalScale, 0.4f);

        if (hintOverlay != null) hintOverlay.SetActive(false);
        if (hintText != null) hintText.SetActive(false);

        isActive = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!isActive) CancelAttentionCue();

        if (!isActive)
            LeanTween.scale(gameObject, originalScale * 1.1f, 0.2f).setEaseOutSine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (!isActive)
            LeanTween.scale(gameObject, originalScale, 0.2f).setEaseInSine();
    }
}
