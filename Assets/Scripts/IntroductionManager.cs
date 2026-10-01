using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroductionManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject npcImage;
    public GameObject speechBubble;
    public TMP_Text dialogueText;
    public Button continueButton;

    [Header("Cloudflare Worker")]
    [Tooltip("Your Worker URL, e.g. https://psych-introduction.dauriagoalie31.workers.dev")]
    public string introductionWorkerUrl = "https://psych-introduction.dauriagoalie31.workers.dev";

    [Tooltip("Request timeout in seconds")]
    public int timeoutSeconds = 20;

    private string currentScenario;
    private bool skipTyping = false;
    private string introText; // null until the saved intro or the worker's reply is in

    void Start()
    {
        currentScenario = PlayerPrefs.GetString("LastScenario", "");

        npcImage.SetActive(false);
        speechBubble.SetActive(false);
        continueButton.gameObject.SetActive(false);

        // Asked for now, while the NPC walks in, rather than once they've
        // arrived: waiting until then left an empty bubble for seconds.
        StartCoroutine(LoadIntro());
        StartCoroutine(StartIntroSequence());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            skipTyping = true;
    }

    IEnumerator StartIntroSequence()
    {
        yield return new WaitForSeconds(0.3f);

        npcImage.SetActive(true);
        Vector3 startPos = npcImage.transform.localPosition;
        npcImage.transform.localPosition = new Vector3(800f, startPos.y, startPos.z);
        LeanTween.moveLocalX(npcImage, startPos.x, 0.8f).setEaseOutBack();

        yield return new WaitForSeconds(0.6f);

        speechBubble.SetActive(true);

        // Still waiting on the worker: "..." so the NPC is clearly about to speak.
        for (float t = 0f; introText == null; t += Time.deltaTime)
        {
            dialogueText.text = new string('.', 1 + (int)(t / 0.35f) % 3);
            yield return null;
        }

        yield return TypeText(introText, () => continueButton.gameObject.SetActive(true));
    }

    IEnumerator LoadIntro()
    {
        string saved = SavedIntro();
        if (saved != null)
        {
            introText = saved;
            yield break;
        }
        yield return RequestWorkerIntro();
    }

    string SavedIntro()
    {
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);
        if (slot == -1 || !SaveManager.HasSave(slot)) return null;

        SaveData data = SaveManager.Load(slot);
        if (data != null && data.npcIntroductions != null &&
            data.npcIntroductions.TryGetValue(currentScenario, out string savedIntro))
        {
            return savedIntro;
        }
        return null;
    }

    IEnumerator RequestWorkerIntro()
    {
        // Fallback if something fails
        string finalText = "Hi, I’m Alex! I really need your help with something important.";

        if (string.IsNullOrWhiteSpace(introductionWorkerUrl))
        {
            introText = finalText;
            yield break;
        }

        // Build payload expected by your worker: { "scenario": "...", "cards": [...] }
        // Your worker accepts cards, but doesn't require them; we can send none.
        WorkerIntroRequest payload = new WorkerIntroRequest
        {
            scenario = currentScenario ?? "",
            cards = GetCardsFromPrefsSanitized()
        };

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest request = new UnityWebRequest(introductionWorkerUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                string intro = TryParseIntro(jsonResponse);

                if (!string.IsNullOrWhiteSpace(intro))
                {
                    finalText = intro.Trim();
                    // Only a real intro is kept: saving the fallback stuck the
                    // save with it even once the worker was back.
                    SaveNPCIntro(finalText);
                }
                else
                    Debug.LogWarning("[IntroductionManager] Worker response did not contain 'intro'. Raw: " + jsonResponse);
            }
            else
            {
                Debug.LogError($"[IntroductionManager] Worker call failed: {request.error}\n{request.downloadHandler.text}");
            }
        }

        introText = finalText;
    }

    private static string TryParseIntro(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            WorkerIntroResponse ok = JsonUtility.FromJson<WorkerIntroResponse>(json);
            if (ok != null && !string.IsNullOrWhiteSpace(ok.intro))
                return ok.intro;

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

    private string[] GetCardsFromPrefsSanitized()
    {
        string cardsRaw = PlayerPrefs.GetString("LastCards", "");
        if (string.IsNullOrEmpty(cardsRaw))
            return Array.Empty<string>();

        List<string> cards = new List<string>();
        foreach (var c in cardsRaw.Split('|'))
        {
            if (!string.IsNullOrWhiteSpace(c))
                cards.Add(c.Trim());
        }
        return cards.ToArray();
    }

    void SaveNPCIntro(string intro)
    {
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);
        if (slot != -1 && SaveManager.HasSave(slot))
        {
            SaveData data = SaveManager.Load(slot);
            if (data == null) return;

            if (data.npcIntroductions == null)
                data.npcIntroductions = new Dictionary<string, string>();

            if (!data.npcIntroductions.ContainsKey(currentScenario))
            {
                data.npcIntroductions[currentScenario] = intro;
                SaveManager.Save(slot, data);
            }
        }
    }

    IEnumerator TypeText(string text, Action onComplete = null)
    {
        // Group 2 (Feature.ThemedTextBoxes): the same letter-by-letter reveal and
        // click-to-finish, but the whole line is laid out up front and uncovered,
        // so words don't jump lines and the bubble is its final size at once.
        if (TextBoxTheme.Enabled)
        {
            skipTyping = false;
            yield return TextPanel.Reveal(dialogueText, text, 0.03f, () => skipTyping);
            onComplete?.Invoke();
            yield break;
        }

        dialogueText.text = "";
        skipTyping = false;

        foreach (char c in text)
        {
            if (skipTyping)
            {
                dialogueText.text = text;
                break;
            }

            dialogueText.text += c;
            yield return new WaitForSeconds(0.03f);
        }

        onComplete?.Invoke();
    }

    public void OnContinueClicked()
    {
        SceneTransition.Load("GameScene");
    }

    // ---------------- DTOs ----------------

    [Serializable]
    private class WorkerIntroRequest
    {
        public string scenario;
        public string[] cards;
    }

    [Serializable]
    private class WorkerIntroResponse
    {
        public string intro;
    }

    [Serializable]
    private class WorkerError
    {
        public string error;
        public string details;
    }
}
