using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Save Prompt")]
    public GameObject savePromptPanel;
    public TMP_InputField saveNameInput;
    public int currentSaveSlot = -1;

    [Header("Game State")]
    public TextMeshProUGUI scenarioText;
    public TextMeshProUGUI[] cardTexts;
    public TMP_InputField responseInput;
    public List<string> currentCards = new List<string>();
    public string currentScenario;

    [Header("Tracking")]
    public List<string> usedVocab = new List<string>(); // PERMANENTLY burned
    public List<ScenarioResponse> responseHistory = new List<ScenarioResponse>();

    private List<string> allVocab = new List<string>();
    private Dictionary<string, string> termToDefinition = new Dictionary<string, string>();

    [Header("Card Refresh")]
    public Button refreshButton;
    public TextMeshProUGUI refreshCounterText;
    private int refreshUsesRemaining = 2;

    private bool isTyping = false;
    private bool skipTyping = false;

    void Update()
    {
        if (isTyping && Input.GetMouseButtonDown(0))
            skipTyping = true;
    }

    void Start()
    {
        LoadVocab();

        currentSaveSlot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);
        bool comingFromGrading = PlayerPrefs.GetInt("FromGrading", 0) == 1;

        if (comingFromGrading)
        {
            currentScenario = PlayerPrefs.GetString("LastScenario", "");

            string cardsString = PlayerPrefs.GetString("LastCards", "");
            currentCards = string.IsNullOrEmpty(cardsString)
                ? new List<string>()
                : new List<string>(cardsString.Split('|'));

            if (currentCards.Count == 0)
                GenerateNewCards();

            PlayerPrefs.SetInt("FromGrading", 0);
            PlayerPrefs.Save();
        }
        else
        {
            if (currentSaveSlot == -1)
            {
                // TRUE new game
                usedVocab.Clear();
                responseHistory.Clear();

                currentScenario = ScenarioSequencer.GetNextScenario();
                GenerateNewCards();
            }
            else if (SaveManager.HasSave(currentSaveSlot))
            {
                SaveData data = SaveManager.Load(currentSaveSlot);

                currentScenario = data.scenario;
                currentCards = data.cards ?? new List<string>();
                usedVocab = data.usedVocab ?? new List<string>();
                responseHistory = data.responses ?? new List<ScenarioResponse>();

                if (currentCards.Count == 0)
                    GenerateNewCards();
            }
            else
            {
                currentScenario = ScenarioSequencer.GetNextScenario();
                GenerateNewCards();
            }
        }

        StartCoroutine(TypeText(scenarioText, currentScenario, 0.03f));

        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].text = i < currentCards.Count ? currentCards[i] : "[Empty]";

        SetCardBacks();

        refreshUsesRemaining = 2;
        UpdateRefreshUI();
    }

    // -------------------- VOCAB --------------------

    void LoadVocab()
    {
        TextAsset vocabAsset = Resources.Load<TextAsset>("cleaned_terms");
        if (vocabAsset == null) return;

        allVocab.Clear();
        termToDefinition.Clear();

        foreach (string line in vocabAsset.text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split(new[] { " - " }, System.StringSplitOptions.None);
            if (parts.Length < 2) continue;

            string term = System.Text.RegularExpressions.Regex
                .Replace(parts[0].Trim(), @"^\d+\s*", "");

            allVocab.Add(term);
            termToDefinition[term] = parts[1].Trim();
        }
    }

    // -------------------- CARD GENERATION --------------------
    // IMPORTANT: DOES NOT burn cards
    void GenerateNewCards()
    {
        currentCards.Clear();

        List<string> pool = new List<string>();
        foreach (string v in allVocab)
            if (!usedVocab.Contains(v))
                pool.Add(v);

        while (currentCards.Count < 5 && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            currentCards.Add(pool[index]);
            pool.RemoveAt(index); // prevent duplicates in same hand
        }

        PlayerPrefs.SetString("LastCards", string.Join("|", currentCards));
        PlayerPrefs.Save();
    }

    void SetCardBacks()
    {
        for (int i = 0; i < currentCards.Count; i++)
        {
            GameObject cardObj = GameObject.Find($"Card{i + 1}");
            if (!cardObj) continue;

            TMP_Text back = cardObj.transform
                .Find($"BackFace/BackText{i + 1}")
                ?.GetComponent<TMP_Text>();

            if (back != null && termToDefinition.ContainsKey(currentCards[i]))
                back.text = termToDefinition[currentCards[i]];
        }
    }

    // -------------------- REFRESH --------------------

    public void RefreshCards()
    {
        if (refreshUsesRemaining <= 0) return;

        GenerateNewCards();
        refreshUsesRemaining--;
        UpdateRefreshUI();

        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].text = i < currentCards.Count ? currentCards[i] : "[Empty]";

        SetCardBacks();
    }

    void UpdateRefreshUI()
    {
        if (refreshButton)
            refreshButton.interactable = refreshUsesRemaining > 0;

        if (refreshCounterText)
            refreshCounterText.text = $"Refreshes Left: {refreshUsesRemaining}";
    }

    // -------------------- SUBMIT (BURN HAPPENS HERE) --------------------

    public void OnSubmitResponse(TMP_InputField field)
    {
        string userResponse = field.text.Trim();

        // Burn ONLY the cards currently visible
        foreach (string card in currentCards)
        {
            if (!usedVocab.Contains(card))
                usedVocab.Add(card);
        }

        responseHistory.Add(new ScenarioResponse
        {
            scenario = currentScenario,
            response = userResponse,
            aiFeedback = ""
        });

        // THIS WAS MISSING
        PlayerPrefs.SetString("LastResponse", userResponse);

        SaveProgress();

        PlayerPrefs.SetString("LastScenario", currentScenario);
        PlayerPrefs.SetString("LastCards", string.Join("|", currentCards));
        PlayerPrefs.SetInt("FromGrading", 1);
        PlayerPrefs.Save();

        field.text = "";
        SceneManager.LoadScene("GradingScene");
    }


    // -------------------- TYPING --------------------

    IEnumerator TypeText(TextMeshProUGUI textObj, string fullText, float delay)
    {
        isTyping = true;
        skipTyping = false;
        textObj.text = "";

        foreach (char c in fullText)
        {
            if (skipTyping)
            {
                textObj.text = fullText;
                break;
            }

            textObj.text += c;
            yield return new WaitForSeconds(delay);
        }

        isTyping = false;
    }

    // -------------------- SAVE / LOAD --------------------

    void SaveProgress()
    {
        SaveManager.Save(currentSaveSlot, new SaveData
        {
            title = SaveManager.GetTitle(currentSaveSlot),
            scenario = currentScenario,
            cards = new List<string>(currentCards),
            usedVocab = new List<string>(usedVocab),
            responses = new List<ScenarioResponse>(responseHistory)
        });
    }

    public void StartNewGame()
    {
        PlayerPrefs.DeleteKey("LastScenario");
        PlayerPrefs.DeleteKey("LastCards");
        PlayerPrefs.DeleteKey("LastResponse");
        PlayerPrefs.DeleteKey("RemainingScenarios");

        PlayerPrefs.SetInt("SelectedSaveSlot", -1);
        PlayerPrefs.SetInt("FromGrading", 0);
        PlayerPrefs.Save();

        usedVocab.Clear();
        responseHistory.Clear();

        SceneManager.LoadScene("LoadingScene");
    }

    public void SetNewGameSlot(int idx)
    {
        SaveManager.SetTempSaveSlot(idx);
        PlayerPrefs.SetInt("SelectedSaveSlot", idx);
        PlayerPrefs.DeleteKey("RemainingScenarios");
        PlayerPrefs.Save();

        SceneManager.LoadScene("LoadingScene");
    }

    public void ContinueGame() => SceneManager.LoadScene("SaveSelectScene");

    public void PromptSave() => savePromptPanel.SetActive(true);

    public void CancelAndExit()
    {
        savePromptPanel.SetActive(false);
        SceneManager.LoadScene("SplashScene");
    }

    public void ConfirmSave()
    {
        // Ensure we have a real slot
        if (currentSaveSlot == -1)
        {
            currentSaveSlot = SaveManager.GetFirstAvailableSlot();
            PlayerPrefs.SetInt("SelectedSaveSlot", currentSaveSlot);
        }

        string name = string.IsNullOrWhiteSpace(saveNameInput.text)
            ? $"Save {currentSaveSlot + 1}"
            : saveNameInput.text;

        SaveData data = new SaveData
        {
            title = name,
            scenario = currentScenario,
            cards = new List<string>(currentCards),
            usedVocab = new List<string>(usedVocab),
            responses = new List<ScenarioResponse>(responseHistory)
        };

        SaveManager.Save(currentSaveSlot, data);

        PlayerPrefs.Save();

        savePromptPanel.SetActive(false);
        SceneManager.LoadScene("SplashScene");
    }

}
