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
    FaceDownCards,            // cards are dealt face-down and flip over in the hand when clicked, staying face-up; every face-up card shows the AI Help button, labelled Hint (CardBehavior, GameManager)
    HoverAnimations,          // buttons pop on hover (ButtonHoverPop)
    RaisedHand,               // the hand is a bit smaller, flatter and higher so no card hangs off the bottom; the response box makes room (GameManager)
    NewCardArt,              // layered card back/front art per area of psychology from Resources/CardArt; placeholders for areas still being drawn (CardBehavior, CardArt, TermAreas, GameManager)
    BackgroundTweening,       // scene backgrounds slowly drift and zoom, except where ShaderBackground replaces them and where the title is painted into the background (BackgroundDrift)
    ShaderBackground,         // the response screen's (GameScene) room image becomes a slow animated shader, Resources/ThoughtCurrents; the grading and review screens get it too (ShaderBackground)
    ImprovedMenuTransitions,  // screens fade out and in between scenes (SceneTransition)
    ThemedTextBoxes,          // the response screen's scenario, Brainy/definition and response boxes are restyled paper panels that fit their text and scroll when long; Brainy steps forward for definitions too; the scenario reveals without reflowing; the intro screen's speech bubble, the rules screen's Brainy bubble (which holds the example too), the grading and review screens' text and the save prompt get the same panels (TextBoxTheme, TextPanel, GameManager, BrainBehavior, CardBehavior, IntroductionManager, RulesManager, GradingManager, ResponseReview)
    PixelButtons,             // every screen's buttons but the title screen's are pixel-art plates like the card backs, from Resources/PixelButtons; the save slots keep their names on blank plates; Continue/Next (green) and Menu (blue) are the title screen's plate (Resources/ButtonPlates) labelled in Righteous in the title screen's ink; Refresh is its own teal plate (Resources/ButtonPlates/Refresh), labelled the same, with a diamond lit in its top tab for each use left (PixelButton, MenuButtonPlates, GameManager)
    SynopsisTransition,       // the intro screen gets a livelier version of the shader background and is laid over the response screen, which waits beneath it (the round, its scenario reveal and its logged start are held); Continue turns one into the other with no fade or scene change: the background calms, the NPC moves to the avatar's spot, then the rest slides in. The click log still counts the synopsis as "introduction" (SynopsisTransition, ShaderBackground, GameManager, CardBehavior, ClickLogger)
    GoodFitCards,             // hands (and refreshes) deal only terms that score 1.5 or better for the current scenario in Resources/scenario_fit.txt (ScenarioFit, GameManager)
    JarBrainy,                // every Brainy is the jar art from Resources (poirot) with a separate moustache that twitches now and then; on the response screen it is drawn a little bigger and wider, with a gentle idle bob and sway (JarBrainy, BrainBehavior, RulesManager, GradingManager)
    SlowGraphicsNotice,       // when the browser is drawing without the graphics card, a small popup after the username prompt says how to turn acceleration on (GraphicsNotice, ClickLogger)
    ScenarioImage,            // the intro screen shows the scenario's picture in a paper frame under the speech bubble, from Resources/ScenarioImages/<scenario number>, else Placeholder (IntroductionManager)
    GradingTransition,        // submitting and leaving the grading screen no longer fade to black: the screen's elements slide out while the shader background turns the next screen's (excited red for grading, the synopsis's excited blue after it), then the next screen loads straight in and its elements slide in (GradingTransition, ShaderBackground, SceneTransition, GameManager, GradingManager, IntroductionManager)
    MenuCardBackground,       // the title screen's room wall becomes Resources/MenuCards, columns of card backs (Resources/MenuCardBacks) scrolling up and down, alternating by column; the rest of the title art (glow, jar, players, title) is hidden so only the cards and buttons show, with the new title (Resources/MenuTitle) on top above them (MenuCardBackground)
    MenuIntro,                // opening on the title screen, card backs swirl in from every edge into a messy pile on the menu's blue, which bursts outwards as the screen flashes white and fades to the menu; the username prompt waits for it (MenuIntro, ClickLogger)
    MenuButtonPlates,         // the title screen's buttons become blank stone plates (Resources/MenuButton) with their labels written on in Righteous (Resources/Fonts/Righteous) (MenuButtonPlates, MenuCardBackground)
    MenuNewGameTransition,    // New Game skips the loading screen: the title and the other buttons slide off, then New Game, then the menu's card backs, and those above the screen, fall straight down with a little drift and turn each, leaving only the blue; the synopsis loads under it and the blue fades into the synopsis's background turning excited, then the synopsis begins (MenuNewGameTransition, MenuCards shader, MenuCardBackground, SplashUI, IntroductionManager)
    JarAvatar,                // the synopsis's NPC is the moustached jar Brainy (as Feature.JarBrainy), centred up the screen, and fades out on Continue; the response screen has no avatar; the synopsis's fallback line names Brainy instead of Alex (JarBrainy, IntroductionManager, GameManager, SynopsisTransition, TextBoxTheme)
    Dictionary,               // a closed book just right of the hand (Resources/DictionaryBook) pops on hover; clicked, it opens (DictionaryBookOpen) and slowly grows to the middle of the screen, where its left page lists the hand's terms (Unknown for cards still face-down) and its right page the chosen card's definition (nothing until the player picks a term; each pick is logged as a "definition" card event); definitions live there instead of on the cards; the book sits just right of the hand; Refresh moves to the middle of the bottom, under the hand, which is lifted clear of it; the book and Menu shrink on narrow screens to stay clear of the hand (TermDictionary, FitBesideHand, TextLinkClicks, GameManager, PixelButton, SynopsisTransition)
    CardHoverGlow,            // a face-down card's edges glow a soft light of its back's color while hovered, as if lit from behind, spilling a little onto the card and tinting the response box's paper too, fading as it flips (CardBehavior, PanelSprites, CardArt, Resources/CardGlowTint)
    GameOverview,             // before the first scenario's synopsis, Brainy first explains the game in the same speech bubble; Continue swaps it for the scenario's picture and synopsis (IntroductionManager)
    GradingTabs,              // with ThemedTextBoxes, the grading screen leads with Brainy's feedback, in a panel fitted to its text beside Brainy and the score (the background shows below it); the scenario and response sit behind two tabs along the top and open above the feedback, pushing it down, when clicked (GradingTabs, TextBoxTheme, TextPanel, GradingTransition)
    TrophyEnd,                // the end screen's picture becomes the shader background, excited, in black and white with a floating pixel-art trophy (Resources/GoldTrophy) and a ring of card backs, one per area, circling it; the title and line from the old picture are written on top (TrophyEnd, ShaderBackground)
    CreditsMenu,              // the title screen's Review button becomes Credits, at the bottom of the buttons, opening a blank screen on the calm shader background with a Menu button back (to be filled in later); Rules is greyed out for now (CreditsScreen, SplashUI, ShaderBackground, ClickLogger)
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
        Feature.ScenarioImage,
        Feature.GradingTransition,
        Feature.MenuCardBackground,
        Feature.MenuIntro,
        Feature.MenuButtonPlates,
        Feature.MenuNewGameTransition,
        Feature.JarAvatar,
        Feature.Dictionary,
        Feature.CardHoverGlow,
        Feature.GameOverview,
        Feature.GradingTabs,
        Feature.TrophyEnd,
        Feature.CreditsMenu,
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
