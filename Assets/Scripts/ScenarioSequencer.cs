using System.Collections.Generic;
using UnityEngine;

public static class ScenarioSequencer
{
    private const string ScenarioListKey = "RemainingScenarios";

    // Wrapper class for JSON
    [System.Serializable]
    public class ScenarioListWrapper
    {
        public List<string> scenarios;
    }

    // Load fresh list OR existing list
    public static List<string> LoadOrCreateScenarioList()
    {
        if (PlayerPrefs.HasKey(ScenarioListKey))
        {
            string json = PlayerPrefs.GetString(ScenarioListKey, "");
            return JsonUtility.FromJson<ScenarioListWrapper>(json).scenarios;
        }

        // Otherwise, load the ordered list from file
        var ordered = LoadScenariosInOrder();
        SaveScenarioList(ordered);
        return ordered;
    }

    // Load scenarios in EXACT file order
    public static List<string> LoadScenariosInOrder()
    {
        TextAsset asset = Resources.Load<TextAsset>("Scenarios");

        string[] rawLines = asset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        List<string> ordered = new List<string>();
        foreach (var line in rawLines)
        {
            string clean = line.Trim();
            if (!string.IsNullOrEmpty(clean))
                ordered.Add(clean);
        }

        return ordered;
    }

    public static void SaveScenarioList(List<string> list)
    {
        ScenarioListWrapper wrap = new ScenarioListWrapper { scenarios = list };
        string json = JsonUtility.ToJson(wrap);
        PlayerPrefs.SetString(ScenarioListKey, json);
        PlayerPrefs.Save();
    }

    // The next GetNextScenario starts from the top of Scenarios.txt again.
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(ScenarioListKey);
    }

    // The round's scenario: the one already chosen (LastScenario), or, at the
    // start of a new game (which clears it), the next one, saved so the
    // synopsis and the response screen agree whichever of them opens first.
    public static string Current()
    {
        string scenario = PlayerPrefs.GetString("LastScenario", "");
        if (!string.IsNullOrEmpty(scenario)) return scenario;

        scenario = GetNextScenario();
        if (scenario != null)
        {
            PlayerPrefs.SetString("LastScenario", scenario);
            PlayerPrefs.Save();
        }
        return scenario;
    }

    // Get next scenario sequentially
    public static string GetNextScenario()
    {
        List<string> remaining = LoadOrCreateScenarioList();

        if (remaining.Count == 0)
            return null;

        string next = remaining[0];   // Always FIRST, sequential order
        remaining.RemoveAt(0);

        SaveScenarioList(remaining);

        return next;
    }
}
