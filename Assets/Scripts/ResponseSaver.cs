using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ResponseSaver : MonoBehaviour
{
    public TextMeshProUGUI scenarioText;
    public TMP_InputField responseInput;
    public TextMeshProUGUI[] cardTexts;

    private List<string> scenarios = new List<string>();
    private List<string> vocabWords = new List<string>();
    private List<string> usedScenarios = new List<string>();
    private List<string> usedVocab = new List<string>();

    private int scenarioIndex = 0; // NEW: track scenario position

    void Start()
    {
        // Ensure a valid save slot is always selected
        if (!PlayerPrefs.HasKey("SelectedSaveSlot"))
        {
            PlayerPrefs.SetInt("SelectedSaveSlot", 0);
            PlayerPrefs.Save();
        }

        LoadTextFiles();
        ShowNext();
    }

    void LoadTextFiles()
    {
        TextAsset scenarioFile = Resources.Load<TextAsset>("Scenarios");
        TextAsset vocabFile = Resources.Load<TextAsset>("VocabWords");

        if (scenarioFile != null)
            scenarios = new List<string>(scenarioFile.text.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries));

        if (vocabFile != null)
            vocabWords = new List<string>(vocabFile.text.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries));

        // Reset index if out of range
        scenarioIndex = Mathf.Clamp(PlayerPrefs.GetInt("ScenarioIndex", 0), 0, Mathf.Max(0, scenarios.Count - 1));
    }

    public void SaveResponse()
    {
        string scenario = scenarioText.text;
        string userResponse = responseInput.text;
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", 0);

        SaveData data = SaveManager.HasSave(slot)
            ? SaveManager.Load(slot)
            : new SaveData
            {
                title = $"Save {slot}",
                scenario = scenario,
                cards = new List<string>(),
                usedScenarios = new List<string>(),
                usedVocab = new List<string>(),
                responses = new List<ScenarioResponse>()
            };

        if (data.responses == null)
            data.responses = new List<ScenarioResponse>();

        data.responses.Add(new ScenarioResponse
        {
            scenario = scenario,
            response = userResponse,
            aiFeedback = ""
        });

        SaveManager.Save(slot, data);

        // Debug confirmation
        SaveData checkData = SaveManager.Load(slot);
        Debug.Log($"[ResponseSaver] Saved to slot {slot}. Total Responses: {checkData.responses?.Count ?? 0}");

        // Move to next scenario
        ShowNext();
    }

    void ShowNext()
    {
        if (scenarios == null || scenarios.Count == 0)
        {
            scenarioText.text = "No scenarios loaded.";
            return;
        }

        // Pick the next scenario in order
        if (scenarioIndex >= scenarios.Count)
        {
            // Reached the end
            scenarioText.text = "All scenarios completed!";
            Debug.Log("[ResponseSaver] All scenarios shown.");
            return;
        }

        string newScenario = scenarios[scenarioIndex];
        scenarioText.text = newScenario;
        usedScenarios.Add(newScenario);

        // Save current index so progress persists if needed
        PlayerPrefs.SetInt("ScenarioIndex", scenarioIndex + 1);
        PlayerPrefs.Save();

        // Increment for next time
        scenarioIndex++;

        // Generate new vocab (still random)
        List<string> nextVocab = new List<string>();
        while (nextVocab.Count < 5 && usedVocab.Count < vocabWords.Count)
        {
            string word = vocabWords[Random.Range(0, vocabWords.Count)];
            if (!usedVocab.Contains(word))
            {
                usedVocab.Add(word);
                nextVocab.Add(word);
            }
        }

        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].text = i < nextVocab.Count ? nextVocab[i] : "[Done]";

        responseInput.text = "";
    }

    public void ResetUsedData()
    {
        usedScenarios.Clear();
        usedVocab.Clear();
        scenarioIndex = 0;
        PlayerPrefs.SetInt("ScenarioIndex", 0);
        PlayerPrefs.Save();
        ShowNext();
    }
}
