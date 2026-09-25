using System;
using System.Collections.Generic;
using UnityEngine;

// Every experimental feature that can differ between test groups. Add a new
// entry here for each new feature, then turn it on for the groups that should
// get it in TestGroups.Groups below.
public enum Feature
{
    BrainyAttentionCue,       // Brainy hops every few seconds while idle (BrainBehavior)
    BrainyBesideResponseBox,  // Brainy rests just left of the response box instead of bottom right (BrainBehavior)
    CardTweening,             // cards deal in, sweep out/in on refresh, and float while idle (CardBehavior, GameManager)
    FaceDownCards,            // cards are dealt face-down and flip over in the hand when clicked, staying face-up; the AI Help button becomes a Definition popup (CardBehavior, GameManager)
    HoverAnimations,          // buttons pop on hover (ButtonHoverPop)
    RaisedHand,               // the hand is a bit smaller, flatter and higher so no card hangs off the bottom; the response box makes room (GameManager)
    NewCardArt,              // layered card back/front art per area of psychology from Resources/CardArt; placeholders for areas still being drawn (CardBehavior, CardArt, TermAreas, GameManager)
    BackgroundTweening,       // scene backgrounds slowly drift and zoom (BackgroundDrift)
    ImprovedMenuTransitions,  // screens fade out and in between scenes (SceneTransition)
}

// The study's conditions, all in one place: which features each test group
// sees. Group 1 is the original game with every feature off (the control).
//
// Participants are assigned by link: .../index.html?group=2. A missing or
// unrecognized group falls back to DefaultGroup. In the Unity Editor, pick
// the group to play as from the PsychGame > Test Group menu.
//
// Game code checks a feature with:
//     if (TestGroups.IsEnabled(Feature.CardTweening)) { ... }
public static class TestGroups
{
    public const int DefaultGroup = 1;

    private static readonly Dictionary<int, HashSet<Feature>> Groups = new Dictionary<int, HashSet<Feature>>
    {
        // Group 1: the original game (control).
        { 1, new HashSet<Feature>() },

        // Group 2: visual polish pass.
        { 2, new HashSet<Feature>
            {
                Feature.BrainyAttentionCue,
                Feature.BrainyBesideResponseBox,
                Feature.CardTweening,
                Feature.FaceDownCards,
                Feature.RaisedHand,
                Feature.NewCardArt,
                Feature.HoverAnimations,
                Feature.BackgroundTweening,
                Feature.ImprovedMenuTransitions,
            }
        },
    };

    public static IEnumerable<int> DefinedGroups => Groups.Keys;

    private static int? _currentGroup;

    // Resolved once, on first use, and fixed for the rest of the session.
    public static int CurrentGroup
    {
        get
        {
            if (_currentGroup == null)
            {
                _currentGroup = ResolveGroup();
                Debug.Log($"[TestGroups] Playing as group {_currentGroup} (features: {EnabledFeaturesString()})");
            }
            return _currentGroup.Value;
        }
    }

    public static bool IsEnabled(Feature feature)
    {
        return Groups[CurrentGroup].Contains(feature);
    }

    // Pipe-separated names of the features on for the current group, e.g.
    // "CardTweening|HoverAnimations", or "" for none. Logged with the session
    // so the data records exactly what each participant saw, even if a
    // group's definition changes partway through the study.
    public static string EnabledFeaturesString()
    {
        var names = new List<string>();
        foreach (Feature f in Enum.GetValues(typeof(Feature)))
        {
            if (Groups[CurrentGroup].Contains(f)) names.Add(f.ToString());
        }
        return string.Join("|", names);
    }

    private static int ResolveGroup()
    {
#if UNITY_EDITOR
        int requested = UnityEditor.EditorPrefs.GetInt(EditorPrefsKey, DefaultGroup);
#else
        int requested = ReadGroupFromUrl() ?? DefaultGroup;
#endif
        if (Groups.ContainsKey(requested)) return requested;

        Debug.LogWarning($"[TestGroups] Group {requested} isn't defined; falling back to group {DefaultGroup}.");
        return DefaultGroup;
    }

    public const string EditorPrefsKey = "PsychGame.EditorTestGroup";

    // Reads ?group=N from the page URL (WebGL). Null when absent or not a number.
    private static int? ReadGroupFromUrl()
    {
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url)) return null;

        int hash = url.IndexOf('#');
        if (hash >= 0) url = url.Substring(0, hash);

        int query = url.IndexOf('?');
        if (query < 0) return null;

        foreach (string pair in url.Substring(query + 1).Split('&'))
        {
            string[] kv = pair.Split('=');
            if (kv.Length == 2 && kv[0].Equals("group", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(kv[1], out int group))
            {
                return group;
            }
        }
        return null;
    }
}
