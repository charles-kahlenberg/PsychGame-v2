using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
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

    [Header("Network")]
    public int timeoutSeconds = 20;

    // ---------------- Public API ----------------

    // Brain hint (scenario + cards)
    public void RequestHint(string scenarioText, List<string> cards)
    {
        List<string> cleanCards = SanitizeCards(cards);

        var payload = new WorkerRequest
        {
            mode = "hint",
            scenario = (scenarioText ?? "").Trim(),
            cards = cleanCards.ToArray(),
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

        if (string.IsNullOrWhiteSpace(workerUrl))
        {
            string msg = "Worker URL is missing.";
            SafeOpenOverlay(msg);
            OnHintReady?.Invoke(msg);
            yield break;
        }

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

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(workerUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[HintManager] Worker call failed: {req.error}\nResp: {req.downloadHandler.text}");
                SafeOpenOverlay(fallbackMessage);
                OnHintReady?.Invoke(fallbackMessage);
                yield break;
            }

            string respText = req.downloadHandler.text;
            string hint = TryParseHint(respText);

            if (string.IsNullOrWhiteSpace(hint))
            {
                Debug.LogWarning("[HintManager] Could not parse worker response. Raw: " + respText);
                SafeOpenOverlay(fallbackMessage);
                OnHintReady?.Invoke(fallbackMessage);
                yield break;
            }

            SafeOpenOverlay(hint.Trim());
            OnHintReady?.Invoke(hint.Trim());
        }
    }

    private static List<string> SanitizeCards(List<string> cards)
    {
        var clean = new List<string>();
        if (cards == null) return clean;

        foreach (var c in cards)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            string trimmed = c.Trim();
            if (!string.IsNullOrEmpty(trimmed)) clean.Add(trimmed);
        }

        return clean;
    }

    private static string TryParseHint(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            WorkerResponse r = JsonUtility.FromJson<WorkerResponse>(json);
            if (r != null && !string.IsNullOrWhiteSpace(r.hint))
                return r.hint;

            WorkerError e = JsonUtility.FromJson<WorkerError>(json);
            if (e != null && !string.IsNullOrWhiteSpace(e.error))
                return e.error;

            return null;
        }
        catch
        {
            return null;
        }
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

    [Serializable]
    private class WorkerResponse
    {
        public string hint;
    }

    [Serializable]
    private class WorkerError
    {
        public string error;
        public string details;
    }
}
