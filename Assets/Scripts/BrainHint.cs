using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BrainHint : MonoBehaviour
{
    public RectTransform brainButton;
    public RectTransform hintBubbleContainer;
    public GameObject hintOverlay;
    public TextMeshProUGUI hintText;

    private Vector3 originalPos;
    private Vector3 originalScale;
    private bool hintShown = false;

    private Coroutine loadingDotsCoroutine;
    private bool isLoading = false;

    private HintManager hintManager;

    void Start()
    {
        originalPos = brainButton.localPosition;
        originalScale = brainButton.localScale;

        hintBubbleContainer.gameObject.SetActive(false);
        if (hintOverlay != null) hintOverlay.SetActive(false);

        hintManager = FindObjectOfType<HintManager>();
        if (hintManager != null)
        {
            hintManager.OnHintReady += OnHintReceived;
        }
    }

    public void OnBrainClicked()
    {
        if (hintShown) return;

        hintShown = true;

        LeanTween.moveLocal(brainButton.gameObject, new Vector3(600f, 0f, 0f), 0.3f).setEaseOutExpo();
        LeanTween.scale(brainButton.gameObject, originalScale * 1.5f, 0.3f).setEaseOutExpo();

        StartCoroutine(ShowHintWithDelay());
    }

    private IEnumerator ShowHintWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (hintOverlay != null) hintOverlay.SetActive(true);
        hintBubbleContainer.gameObject.SetActive(true);

        isLoading = true;
        loadingDotsCoroutine = StartCoroutine(AnimateLoadingDots());

        if (hintManager != null)
        {
            string scenario = PlayerPrefs.GetString("LastScenario", "Missing scenario");

            // Sanitize cards from PlayerPrefs
            string cardsRaw = PlayerPrefs.GetString("LastCards", "");
            List<string> cards = new List<string>();

            if (!string.IsNullOrEmpty(cardsRaw))
            {
                string[] parts = cardsRaw.Split('|');
                foreach (var p in parts)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        cards.Add(p.Trim());
                }
            }

            hintManager.RequestHint(scenario, cards);
        }
        else
        {
            isLoading = false;
            hintText.text = "Hint system not found.";
        }
    }

    private void OnHintReceived(string hint)
    {
        isLoading = false;
        if (loadingDotsCoroutine != null)
        {
            StopCoroutine(loadingDotsCoroutine);
            loadingDotsCoroutine = null;
        }

        hintText.text = !string.IsNullOrEmpty(hint) ? hint : "No hint available.";
    }

    private IEnumerator AnimateLoadingDots()
    {
        string baseText = "Please Wait";
        int dotCount = 0;

        while (isLoading)
        {
            hintText.text = baseText + new string('.', dotCount);
            dotCount = (dotCount + 1) % 4;
            yield return new WaitForSeconds(0.3f);
        }
    }
}
