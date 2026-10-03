using System.Collections.Generic;
using UnityEngine;

// How well each term fits each scenario (1 = made for it, 5 = no way),
// read from Resources/scenario_fit.txt ("Term - 1, 2.5, ..." per line, one
// score per scenario in Scenarios.txt order, # for comments).
public static class ScenarioFit
{
    private const string ResourcePath = "scenario_fit";

    // Feature.GoodFitCards deals only terms scoring this or better.
    public const float GoodFit = 2.5f;

    private static Dictionary<string, float[]> _byTerm;

    // True if the term scores GoodFit or better for the scenario. Unlisted
    // terms (the sheet's dropped ones) and unknown scenarios never fit.
    public static bool Fits(string term, string scenario)
    {
        if (_byTerm == null) Load();
        int index = ScenarioSequencer.LoadScenariosInOrder().IndexOf((scenario ?? "").Trim());
        return index >= 0 && _byTerm.TryGetValue(TermAreas.Key(term), out var scores) &&
               index < scores.Length && scores[index] <= GoodFit;
    }

    private static void Load()
    {
        _byTerm = new Dictionary<string, float[]>();

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[ScenarioFit] Missing Resources/{ResourcePath}.txt");
            return;
        }

        foreach (string raw in asset.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            int split = line.LastIndexOf(" - ");
            if (split < 0) continue;

            var scores = new List<float>();
            foreach (string n in line.Substring(split + 3).Split(','))
                scores.Add(float.Parse(n.Trim(), System.Globalization.CultureInfo.InvariantCulture));
            _byTerm[TermAreas.Key(line.Substring(0, split))] = scores.ToArray();
        }
    }
}
