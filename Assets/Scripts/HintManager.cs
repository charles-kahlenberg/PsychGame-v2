using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintManager : MonoBehaviour
{
    [Header("Cloudflare Worker")]
    [Tooltip("Example: https://psych-hints.dauriagoalie31.workers.dev")]
    public string workerUrl = "https://psych-hints.dauriagoalie31.workers.dev";

    [Header("UI")]
    public TMP_Text hintText;
    public GameObject hintOverlay;
    public ScrollRect hintScrollView;

    // BrainHint subscribes to this
    public Action<string> OnHintReady;

    // ---------------- Public API ----------------

    // Brain hint (scenario + cards)
    public void RequestHint(string scenarioText, List<string> cards)
    {
        var payload = new WorkerRequest
        {
            mode = "hint",
            scenario = (scenarioText ?? "").Trim(),
            cards = Worker.Cards(cards).ToArray(),
            concept = "" // not used for hint
        };

        StartCoroutine(PostToWorker(payload, fallbackMessage: "Failed to load hint. Please try again."));
    }

    // Concept help (scenario + single concept)
    public void RequestConceptHelp(string scenario, string concept)
    {
        var payload = new WorkerRequest
        {
            mode = "concept",
            scenario = (scenario ?? "").Trim(),
            cards = Array.Empty<string>(), // not needed for concept help
            concept = (concept ?? "").Trim()
        };

        StartCoroutine(PostToWorker(payload, fallbackMessage: "Failed to load help. Please try again."));
    }

    // ---------------- Internals ----------------

    private IEnumerator PostToWorker(WorkerRequest payload, string fallbackMessage)
    {
        SafeOpenOverlay("Please wait...");

        // Basic validation
        if (string.IsNullOrWhiteSpace(payload.scenario))
        {
            string msg = "Missing scenario.";
            SafeOpenOverlay(msg);
            OnHintReady?.Invoke(msg);
            yield break;
        }

        if (payload.mode == "hint" && (payload.cards == null || payload.cards.Length == 0))
        {
            string msg = "Missing cards.";
            SafeOpenOverlay(msg);
            OnHintReady?.Invoke(msg);
            yield break;
        }

        if (payload.mode == "concept" && string.IsNullOrWhiteSpace(payload.concept))
        {
            string msg = "Missing concept.";
            SafeOpenOverlay(msg);
            OnHintReady?.Invoke(msg);
            yield break;
        }

        string resp = null;
        yield return Worker.Post(workerUrl, JsonUtility.ToJson(payload), r => resp = r);

        string hint = Worker.Read(resp, r => r.hint);
        if (string.IsNullOrWhiteSpace(hint))
        {
            if (resp != null) Debug.LogWarning("[HintManager] Could not parse worker response. Raw: " + resp);
            SafeOpenOverlay(fallbackMessage);
            OnHintReady?.Invoke(fallbackMessage);
            yield break;
        }

        string trimmedHint = hint.Trim();
        ClickLogger.LogAiResponse(payload.mode == "concept" ? "concept_help" : "hint", payload.scenario, trimmedHint);

        SafeOpenOverlay(trimmedHint);
        OnHintReady?.Invoke(trimmedHint);
    }

    private void SafeOpenOverlay(string text)
    {
        if (hintText != null) hintText.text = text;

        // If something else already opened the overlay, great. If not, opening it is fine.
        if (hintOverlay != null && !hintOverlay.activeSelf)
            hintOverlay.SetActive(true);

        Canvas.ForceUpdateCanvases();
        if (hintScrollView != null)
            hintScrollView.verticalNormalizedPosition = 1f;
    }

    // ---------------- DTOs ----------------

    [Serializable]
    private class WorkerRequest
    {
        public string mode;      // "hint" or "concept"
        public string scenario;
        public string[] cards;   // used for "hint"
        public string concept;   // used for "concept"
    }
}
