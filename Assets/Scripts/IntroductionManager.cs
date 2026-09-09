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
    private Coroutine typingCoroutine;
    private bool skipTyping = false;

    void Start()
    {
        currentScenario = PlayerPrefs.GetString("LastScenario", "");

        npcImage.SetActive(false);
        speechBubble.SetActive(false);
        continueButton.gameObject.SetActive(false);

        StartCoroutine(StartIntroSequence());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            skipTyping = true;
    }

    IEnumerator StartIntroSequence()
    {
        yield return new WaitForSeconds(0.5f);

        npcImage.SetActive(true);
        Vector3 startPos = npcImage.transform.localPosition;
        npcImage.transform.localPosition = new Vector3(800f, startPos.y, startPos.z);
        LeanTween.moveLocalX(npcImage, startPos.x, 0.8f).setEaseOutBack();

        yield return new WaitForSeconds(1f);

        speechBubble.SetActive(true);

        // Try cached intro first
        if (!TryLoadSavedIntro())
            yield return StartCoroutine(RequestWorkerIntro());
    }

    bool TryLoadSavedIntro()
    {
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);
        if (slot == -1) return false;

        if (SaveManager.HasSave(slot))
        {
            SaveData data = SaveManager.Load(slot);

            if (data != null && data.npcIntroductions != null &&
                data.npcIntroductions.ContainsKey(currentScenario))
            {
                string savedIntro = data.npcIntroductions[currentScenario];
                typingCoroutine = StartCoroutine(TypeText(savedIntro, () =>
                {
                    continueButton.gameObject.SetActive(true);
                }));
                return true;
            }
        }
        return false;
    }

    IEnumerator RequestWorkerIntro()
    {
        // Fallback if something fails
        string finalText = "Hi, I’m Alex! I really need your help with something important.";

        if (string.IsNullOrWhiteSpace(introductionWorkerUrl))
        {
            typingCoroutine = StartCoroutine(TypeText(finalText, () => continueButton.gameObject.SetActive(true)));
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
                    finalText = intro.Trim();
                else
                    Debug.LogWarning("[IntroductionManager] Worker response did not contain 'intro'. Raw: " + jsonResponse);
            }
            else
            {
                Debug.LogError($"[IntroductionManager] Worker call failed: {request.error}\n{request.downloadHandler.text}");
            }
        }

        typingCoroutine = StartCoroutine(TypeText(finalText, () => continueButton.gameObject.SetActive(true)));
        SaveNPCIntro(finalText);
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
        SceneManager.LoadScene("GameScene");
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
