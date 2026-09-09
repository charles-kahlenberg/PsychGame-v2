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
