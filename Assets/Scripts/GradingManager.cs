using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GradingManager : MonoBehaviour
{
    public TMP_Text scenarioText;
    public TMP_Text userResponseText;
    public TMP_Text aiResponseText;
    public TMP_Text scoreText;

    [Header("UI")]
    public ScrollRect aiResponseScrollView;

    [Header("Cloudflare Worker")]
    [Tooltip("Example: https://psych-grading.dauriagoalie31.workers.dev")]
    public string gradingWorkerUrl = "https://psych-grading.dauriagoalie31.workers.dev";

    [Tooltip("Request timeout in seconds")]
    public int timeoutSeconds = 20;

    private Coroutine loadingDotsCoroutine;
    private bool isLoading = false;

    private const int MAX_WORDS = 110;

    void Start()
    {
        string scenario = PlayerPrefs.GetString("LastScenario", "Missing scenario");
        string userResponse = PlayerPrefs.GetString("LastResponse", "Missing response");

        // Sanitize cards from prefs
        string cardsRaw = PlayerPrefs.GetString("LastCards", "");
        List<string> cards = new List<string>();
        if (!string.IsNullOrEmpty(cardsRaw))
        {
            foreach (var c in cardsRaw.Split('|'))
            {
                if (!string.IsNullOrWhiteSpace(c))
                    cards.Add(c.Trim());
            }
        }

        scenarioText.text =
            $"<b><color=#000000>Scenario:\n</color></b><color=#FFFFFF>{scenario}</color>";

        userResponseText.text =
            $"<b><color=#000000>Your Response:\n</color></b><color=#FFFFFF>{userResponse}</color>";

        aiResponseText.text =
            "<b><color=#000000>Brainy's Feedback:\n</color></b><color=#FFFFFF>Please Wait</color>";

        StartCoroutine(SendGradingPrompt(scenario, userResponse, cards));
    }

    private IEnumerator SendGradingPrompt(string scenario, string userResponse, List<string> cards)
    {
        // Match used cards based on your existing logic
        string simplifiedResponse = NormalizeText(userResponse);
        List<string> usedCards = new List<string>();

        foreach (string card in cards)
        {
            if (string.IsNullOrWhiteSpace(card)) continue;

            string normalizedCard = NormalizeText(card);
            if (!string.IsNullOrEmpty(normalizedCard) && simplifiedResponse.Contains(normalizedCard))
                usedCards.Add(card.Trim());
        }

        // Start loading animation
        isLoading = true;
        if (loadingDotsCoroutine == null)
            loadingDotsCoroutine = StartCoroutine(AnimateLoadingDots());

        string feedback = null;
        yield return StartCoroutine(PostToGradingWorker(scenario, userResponse, usedCards, (result) => feedback = result));

        // Stop loading animation
        isLoading = false;
        if (loadingDotsCoroutine != null)
        {
            StopCoroutine(loadingDotsCoroutine);
            loadingDotsCoroutine = null;
        }

        if (string.IsNullOrEmpty(feedback))
        {
            aiResponseText.text = "<b>Brainy's Feedback:</b>\nFailed to get feedback.";
            yield break;
        }

        string trimmedFeedback = feedback.Trim();

        aiResponseText.text =
            $"<b><color=#000000>Brainy's Feedback:</color></b><color=#FFFFFF>{trimmedFeedback}</color>";

        ExtractScore(trimmedFeedback);

        Canvas.ForceUpdateCanvases();
        if (aiResponseScrollView != null)
            aiResponseScrollView.verticalNormalizedPosition = 1f;

        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);

        // Update save with feedback (UNCHANGED logic)
        if (SaveManager.HasSave(slot))
        {
            SaveData data = SaveManager.Load(slot);

            if (data.responses != null && data.responses.Count > 0)
                data.responses[data.responses.Count - 1].aiFeedback = trimmedFeedback;

            SaveManager.Save(slot, data);
        }
        else if (slot == -1)
        {
            SaveData tempData = SaveManager.HasSave(-1)
                ? SaveManager.Load(-1)
                : new SaveData
                {
                    title = "Temp Save",
                    scenario = scenario,
                    cards = cards,
                    usedScenarios = new List<string>(),
                    usedVocab = new List<string>(),
                    responses = new List<ScenarioResponse>()
                };

            if (tempData.responses == null)
                tempData.responses = new List<ScenarioResponse>();

            ScenarioResponse existing =
                tempData.responses.Find(r => r.scenario == scenario);

            if (existing == null)
                tempData.responses.Add(new ScenarioResponse
                {
                    scenario = scenario,
                    response = userResponse,
                    aiFeedback = trimmedFeedback
                });
            else
                existing.aiFeedback = trimmedFeedback;

            SaveManager.Save(-1, tempData);
        }
    }

    private IEnumerator PostToGradingWorker(string scenario, string userResponse, List<string> usedCards, Action<string> onDone)
    {
        if (string.IsNullOrWhiteSpace(gradingWorkerUrl))
        {
            onDone?.Invoke("");
            yield break;
        }

        WorkerGradeRequest payload = new WorkerGradeRequest
        {
            scenario = scenario ?? "",
            userResponse = userResponse ?? "",
            usedCards = usedCards != null ? usedCards.ToArray() : Array.Empty<string>()
        };

        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest req = new UnityWebRequest(gradingWorkerUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GradingManager] Worker failed: {req.error}\n{req.downloadHandler.text}");
                onDone?.Invoke("");
                yield break;
            }

            string resp = req.downloadHandler.text;
            string feedback = TryParseFeedback(resp);

            onDone?.Invoke(feedback ?? "");
        }
    }

    private static string TryParseFeedback(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            WorkerGradeResponse ok = JsonUtility.FromJson<WorkerGradeResponse>(json);
            if (ok != null && !string.IsNullOrWhiteSpace(ok.feedback))
                return ok.feedback;

            WorkerError err = JsonUtility.FromJson<WorkerError>(json);
            if (err != null && !string.IsNullOrWhiteSpace(err.error))
                return err.error;

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void OnNextPressed()
    {
        string nextScenario = ScenarioSequencer.GetNextScenario();

        if (string.IsNullOrEmpty(nextScenario))
        {
            Debug.Log("[GradingManager] All scenarios completed — showing GameEndScene.");
            SceneManager.LoadScene("GameEndScene");
            return;
        }

        PlayerPrefs.SetString("LastScenario", nextScenario);
        PlayerPrefs.SetString("LastCards", "");
        PlayerPrefs.SetInt("FromGrading", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene("IntroductionScene");
    }

    private void ExtractScore(string content)
    {
        if (scoreText == null)
            return;

        if (string.IsNullOrEmpty(content))
        {
            scoreText.text = "Score: N/A";
            return;
        }

        Match m = Regex.Match(
            content,
            @"(?i)(?:score(?:d)?|earned)?\s*[:\-]?\s*([0-9]{1,3})\s*(?:out of|/)\s*100");

        if (m.Success &&
            int.TryParse(m.Groups[1].Value, out int score))
        {
            scoreText.text = $"Score: {score}/100";
        }
        else
        {
            scoreText.text = "Score: N/A";
        }
    }

    private IEnumerator AnimateLoadingDots()
    {
        string baseText =
            "<b><color=#000000>Brainy's Feedback:</color></b> " +
            "<color=#FFFFFF>Please Wait</color>";

        int dotCount = 0;

        while (isLoading)
        {
            aiResponseText.text = baseText + new string('.', dotCount);
            dotCount = (dotCount + 1) % 4;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        string lower = input.ToLower();
        string noPunct = Regex.Replace(lower, @"[^\w\s]", " ");
        return Regex.Replace(noPunct, @"\s+", " ").Trim();
    }

    [Serializable]
    private class WorkerGradeRequest
    {
        public string scenario;
        public string userResponse;
        public string[] usedCards;
    }

    [Serializable]
    private class WorkerGradeResponse
    {
        public string feedback;
    }

    [Serializable]
    private class WorkerError
    {
        public string error;
        public string details;
    }
}
