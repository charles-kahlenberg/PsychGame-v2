using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BrainBehavior : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 originalPos;
    private Vector3 originalScale;
    private bool isActive = false;

    public Vector3 focusPosition = new Vector3(500f, 0f, 0f);
    public GameObject hintOverlay;
    public GameObject hintText;
    public TMP_Text scenarioText;
    public HintManager hintManager;

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;

        if (hintOverlay != null) hintOverlay.SetActive(false);
        if (hintText != null) hintText.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isActive) return;

        isActive = true;

        LeanTween.moveLocal(gameObject, focusPosition, 0.4f).setEaseOutExpo();
        LeanTween.scale(gameObject, originalScale * 1.3f, 0.4f).setEaseOutBack();

        Invoke(nameof(ShowHint), 0.5f);
    }

    void ShowHint()
    {
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

    void Update()
    {
        if (isActive && Input.GetMouseButtonDown(0))
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
        if (!isActive)
            LeanTween.scale(gameObject, originalScale * 1.1f, 0.2f).setEaseOutSine();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isActive)
            LeanTween.scale(gameObject, originalScale, 0.2f).setEaseInSine();
    }
}
