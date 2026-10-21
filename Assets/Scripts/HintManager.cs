using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HintManager : MonoBehaviour
{
    [Header("Cloudflare Worker")]
    [Tooltip("Example: https://psych-ai.charliekahlenberg.workers.dev/hint")]
    public string workerUrl = "https://psych-ai.charliekahlenberg.workers.dev/hint";

    [Header("UI")]
    public TMP_Text hintText;
    public GameObject hintOverlay;
    public ScrollRect hintScrollView;

    // BrainHint subscribes to this
    public Action<string> OnHintReady;

    // One click on Brainy asks twice: its Button calls BrainHint, and
    // BrainBehavior handles the same click. The second ask is dropped while
    // the first is out, or the player got (and the log counted) two hints.
    private bool hintPending;

    // Each answer, by the request that got it: asking again (same scenario
    // and cards, or the same card's concept) shows it without a new query.
    // Static, so it outlasts leaving and coming back to the scene. Failures
    // aren't kept, so those are asked again.
    private static readonly Dictionary<string, string> Answers = new Dictionary<string, string>();

    // ---------------- Public API ----------------

    // Brain hint (scenario + cards)
    public void RequestHint(string scenarioText, List<string> cards)
    {
        if (hintPending) return;

        var payload = new WorkerRequest
        {
            mode = "hint",
            scenario = (scenarioText ?? "").Trim(),
            cards = Worker.Cards(cards).ToArray(),
            concept = "" // not used for hint
        };

        StartCoroutine(PostHint(payload));
    }

    private IEnumerator PostHint(WorkerRequest payload)
    {
        hintPending = true;
        yield return PostToWorker(payload, fallbackMessage: "Failed to load hint. Please try again.");
        hintPending = false;
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

        string json = JsonUtility.ToJson(payload);
        if (!Answers.TryGetValue(json, out string trimmedHint))
        {
            string resp = null;
            yield return Worker.Post(workerUrl, json, r => resp = r, tries: 3);

            string hint = Worker.Read(resp, r => r.hint);
            if (string.IsNullOrWhiteSpace(hint))
            {
                if (resp != null) Debug.LogWarning("[HintManager] Could not parse worker response. Raw: " + resp);
                SafeOpenOverlay(fallbackMessage);
                OnHintReady?.Invoke(fallbackMessage);
                yield break;
            }

            trimmedHint = hint.Trim();
            Answers[json] = trimmedHint;
            ClickLogger.LogAiResponse(payload.mode == "concept" ? "concept_help" : "hint", payload.scenario, trimmedHint);
        }

        SafeOpenOverlay(trimmedHint);
        OnHintReady?.Invoke(trimmedHint);
    }

    private void SafeOpenOverlay(string text)
    {
        if (hintText != null) hintText.text = text;

        // Callers open the overlay themselves. Never reopen it here: a reply
        // landing after the player closed the bubble would leave the screen
        // dimmed with no bubble.

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
