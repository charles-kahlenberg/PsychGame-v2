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
    BackgroundTweening,       // scene backgrounds slowly drift and zoom, except where ShaderBackground replaces them and where the title is painted into the background (BackgroundDrift)
    ShaderBackground,         // the response screen's (GameScene) room image becomes a slow animated shader, Resources/ThoughtCurrents; the grading and review screens get it too (ShaderBackground)
    ImprovedMenuTransitions,  // screens fade out and in between scenes (SceneTransition)
    ThemedTextBoxes,          // the response screen's scenario, Brainy/definition and response boxes are restyled paper panels that fit their text and scroll when long; Brainy steps forward for definitions too; the scenario reveals without reflowing; the intro screen's speech bubble, the rules screen's Brainy bubble (which holds the example too), the grading and review screens' text and the save prompt get the same panels (TextBoxTheme, TextPanel, GameManager, BrainBehavior, CardBehavior, IntroductionManager, RulesManager, GradingManager, ResponseReview)
    PixelButtons,             // every screen's buttons but the title screen's are pixel-art plates like the card backs, from Resources/PixelButtons; Refresh shows its uses left as diamonds, and the save slots keep their names on blank plates (PixelButton, GameManager)
    SynopsisTransition,       // the intro screen gets a livelier version of the shader background and is laid over the response screen, which waits beneath it (the round, its scenario reveal and its logged start are held); Continue turns one into the other with no fade or scene change: the background calms, the NPC moves to the avatar's spot, then the rest slides in. The click log still counts the synopsis as "introduction" (SynopsisTransition, ShaderBackground, GameManager, CardBehavior, ClickLogger)
    GoodFitCards,             // hands (and refreshes) deal only terms that score 2.5 or better for the current scenario in Resources/scenario_fit.txt (ScenarioFit, GameManager)
    JarBrainy,                // Brainy is the jar art from Resources: poirotm (moustache drawn on) on the rules and grading screens; on the response screen poirot with a separate moustache that twitches now and then, drawn a little bigger and wider, with a gentle idle bob and sway (BrainBehavior, RulesManager, GradingManager)
    SlowGraphicsNotice,       // when the browser is drawing without the graphics card, a small popup after the username prompt says how to turn acceleration on (GraphicsNotice, ClickLogger)
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

    // Group 2's visual polish pass. Group 3 gets all of it too, so UI
    // features added here reach both groups.
    private static readonly Feature[] PolishedUI =
    {
        Feature.BrainyAttentionCue,
        Feature.BrainyBesideResponseBox,
        Feature.CardTweening,
        Feature.FaceDownCards,
        Feature.RaisedHand,
        Feature.NewCardArt,
        Feature.HoverAnimations,
        Feature.BackgroundTweening,
        Feature.ShaderBackground,
        Feature.ImprovedMenuTransitions,
        Feature.ThemedTextBoxes,
        Feature.PixelButtons,
        Feature.SynopsisTransition,
        Feature.JarBrainy,
        Feature.SlowGraphicsNotice,
    };

    private static readonly Dictionary<int, HashSet<Feature>> Groups = new Dictionary<int, HashSet<Feature>>
    {
        // Group 1: the original game (control).
        { 1, new HashSet<Feature>() },

        // Group 2: visual polish pass.
        { 2, new HashSet<Feature>(PolishedUI) },

        // Group 3: group 2, but only dealt cards that fit the scenario well.
        { 3, new HashSet<Feature>(PolishedUI) { Feature.GoodFitCards } },
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
