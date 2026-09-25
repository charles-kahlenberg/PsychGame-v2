using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// The areas of psychology a term can belong to. The numbers match the "Area"
// column of Words_by_Chapter-Composite-final.xlsx.
public enum PsychArea
{
    ResearchMethods = 1,
    Biopsychology = 2,
    Cognitive = 3,
    Developmental = 4,
    Social = 5,
    Clinical = 6,
    Learning = 7,
}

// Which area(s) each term belongs to, read from Resources/term_areas.txt
// ("Term - 2, 3, 6" per line, # for comments).
public static class TermAreas
{
    private const string ResourcePath = "term_areas";

    private static Dictionary<string, PsychArea[]> _byTerm;

    // The term's areas, or an empty array if it isn't listed.
    public static PsychArea[] Get(string term)
    {
        if (_byTerm == null) Load();
        return _byTerm.TryGetValue(Key(term), out var areas) ? areas : new PsychArea[0];
    }

    // One of the term's areas, chosen at random when it has several. Null if
    // the term isn't listed.
    public static PsychArea? PickArea(string term)
    {
        var areas = Get(term);
        if (areas.Length == 0) return null;
        return areas[Random.Range(0, areas.Length)];
    }

    private static void Load()
    {
        _byTerm = new Dictionary<string, PsychArea[]>();

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[TermAreas] Missing Resources/{ResourcePath}.txt");
            return;
        }

        foreach (string raw in asset.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            int split = line.LastIndexOf(" - ");
            if (split < 0) continue;

            var areas = new List<PsychArea>();
            foreach (string n in line.Substring(split + 3).Split(','))
            {
                if (int.TryParse(n.Trim(), out int a) && System.Enum.IsDefined(typeof(PsychArea), a))
                    areas.Add((PsychArea)a);
            }
            if (areas.Count > 0) _byTerm[Key(line.Substring(0, split))] = areas.ToArray();
        }
    }

    // Ignores case, spacing and punctuation, so "Long-term Memory" and
    // "Long Term Memory" (or curly vs straight apostrophes) still match.
    private static string Key(string term) =>
        Regex.Replace((term ?? "").ToLowerInvariant(), "[^a-z0-9]", "");
}
