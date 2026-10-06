using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.GradingTransition (Group 2): the response screen, the grading
// screen and the next synopsis turn into each other like SynopsisTransition,
// with no fade to black, and no scene load in the middle of a move:
//  0. Whenever GameScene loads, GradingScene is tucked in with it, hidden
//     (Tuck). GradingManager holds back its request until it's shown
//     (Holding / Shown).
//  1. Submit: the response screen's elements slide back out the way they
//     came in (the cards drop, the avatar steps off to the right) while the
//     background gets excited again, in red (ShaderBackground.Heat). Then the
//     response screen is swapped for the grading screen on the same
//     background, and its panels slide in.
//  2. Next: the next round (GameScene, with its synopsis and its own tucked
//     grading screen) loads straight away, under this grading screen, which
//     is kept over it (Cover): the load's stall lands on the click, while
//     nothing is moving yet. Then the panels slide out while the background
//     turns the synopsis's blue, and the synopsis begins underneath: its NPC
//     walks in and its speech bubble drops in from the top.
public class GradingTransition : MonoBehaviour
{
    private const float OutTime = 1.8f;
    private const float InTime = 1.2f;
    private const float SlideTime = 0.65f;
    private const float CardsDelay = 0.1f;
    private const int CoverSortingOrder = 200; // over the intro, below SceneTransition's fade

    private static Transform _grading; // GradingScene's canvas, tucked under the response screen
    private static bool _showing;      // the grading screen is up (for the click log)
    private static Transform _cover;   // the last grading screen, over the next synopsis
    private static bool _leaving;

    public static bool Enabled => TestGroups.IsEnabled(Feature.GradingTransition);

    // True while the grading screen is tucked away: GradingManager waits for Shown.
    public static bool Holding => _grading != null;
    public static event Action Shown;

    // True while the last grading screen still covers the synopsis:
    // IntroductionManager waits for it to go.
    public static bool Covering => _cover != null;

    // Whether GameScene is (for now) showing grading, for the click log.
    public static bool ShowsGrading(Scene scene) => _showing && scene.name == "GameScene";

    // The response screen's leavers on top of what slid in with SynopsisTransition.
    private static readonly (string name, Vector2 side, float delay)[] GameOuts =
    {
        ("Avatar", Vector2.right, 0.1f),
        ("HintBubbleContainer", Vector2.left, 0.18f),
    };

    // The grading screen's elements (or ThemedTextBoxes' panels standing in
    // for them), the side they come from and go to, and when they set off.
    private static readonly (string name, Vector2 side, float delay)[] GradingSlides =
    {
        ("ScenarioTextScrollView", Vector2.up, 0f),
        ("UserResponseTextScrollView", Vector2.left, 0.1f),
        ("AIResponseScrollView", Vector2.left, 0.18f),
        ("Brainy", Vector2.right, 0.22f),
        ("Image", Vector2.right, 0.28f), // Brainy's score bubble
        ("NextButton", Vector2.down, 0.4f),
    };

    // -------------------- SETUP --------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "GameScene" && mode == LoadSceneMode.Single)
            {
                _leaving = _showing = false;
                _grading = null;
                Shown = null; // anything left from a grading screen that was never shown
                SceneManager.LoadScene("GradingScene", LoadSceneMode.Additive);
            }
            else if (scene.name == "GradingScene" && mode == LoadSceneMode.Additive)
            {
                Tuck(scene);
            }
        };
    }

    // The grading screen, loaded and laid out but neither drawn nor
    // clickable (ShaderBackground doesn't render on a hidden canvas). Its own
    // camera and event system stand down; the response screen's do the work.
    private static void Tuck(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out Canvas canvas))
            {
                canvas.enabled = false;
                if (root.TryGetComponent(out GraphicRaycaster raycaster)) raycaster.enabled = false;
                _grading = root.transform;
            }
            else if (root.GetComponent<Camera>() != null || root.GetComponent<EventSystem>() != null)
            {
                root.SetActive(false);
            }
        }
    }

    // -------------------- 1. RESPONSE SCREEN -> GRADING --------------------

    public static IEnumerator ToGrading()
    {
        if (_leaving) yield break;
        if (_grading == null)
        {
            SceneTransition.Load("GradingScene"); // nothing tucked in: just go there
            yield break;
        }
        _leaving = true;

        Transform game = RootCanvas(SceneManager.GetActiveScene());
        BlockClicks(game);
        foreach (var (name, side, delay) in SynopsisTransition.SlideIns) SlideOut(game, name, side, delay);
        foreach (var (name, side, delay) in GameOuts) SlideOut(game, name, side, delay);

        var cards = FindObjectsByType<CardBehavior>(FindObjectsSortMode.None);
        Array.Sort(cards, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < cards.Length; i++) cards[i].SweepOut(CardsDelay + i * CardBehavior.DealStagger);

        // Excited and red together: at no excitement the red doesn't show.
        yield return Background(k => ShaderBackground.Excite = ShaderBackground.Heat = k);

        // All in one frame: the response screen (by now just its background)
        // goes, and the grading screen comes on with its panels off screen.
        Transform grading = _grading;
        _grading = null;
        game.gameObject.SetActive(false);
        grading.GetComponent<Canvas>().enabled = true;
        if (grading.TryGetComponent(out GraphicRaycaster raycaster)) raycaster.enabled = true;
        SynopsisTransition.SetBackgroundPaused(grading.gameObject.scene, false); // renders it now

        _showing = true;
        ClickLogger.SwitchScreen("GradingScene");

        Action shown = Shown;
        Shown = null;
        shown?.Invoke(); // GradingManager fills in the panels and asks for feedback

        Destroy(BlockClicks(grading), InTime);
        foreach (var (name, side, delay) in GradingSlides)
        {
            Transform t = Find(grading, name);
            if (t != null) SlideIn(t, side, delay);
        }
        _leaving = false;
    }

    // -------------------- 2. GRADING -> NEXT SYNOPSIS --------------------

    public static void ToSynopsis(Scene gradingScene, string synopsisScene)
    {
        if (_leaving) return;
        Transform canvas = RootCanvas(gradingScene);
        if (canvas == null)
        {
            SceneTransition.Load(synopsisScene);
            return;
        }
        _leaving = true;

        BlockClicks(canvas);
        canvas.GetComponent<Canvas>().sortingOrder = CoverSortingOrder;
        DontDestroyOnLoad(canvas.gameObject);
        _cover = canvas;
        canvas.gameObject.AddComponent<GradingTransition>().StartCoroutine(Cover(canvas, synopsisScene));
    }

    private static IEnumerator Cover(Transform canvas, string synopsisScene)
    {
        SceneTransition.Cut(synopsisScene);

        // Wait out the loads (the intro comes a frame after GameScene), and
        // then the long frame they made, so nothing starts with a jump. The
        // new screens set the look to the intro's; it stays red until it turns.
        Scene intro;
        do
        {
            ShaderBackground.Excite = ShaderBackground.Heat = 1f;
            yield return null;
            intro = SceneManager.GetSceneByName("IntroductionScene");
        } while (!intro.isLoaded);
        ShaderBackground.Heat = 1f;
        yield return null;

        // The intro's background is hidden under this one until it goes.
        SynopsisTransition.SetBackgroundPaused(intro, true);
        foreach (var (name, side, delay) in GradingSlides) SlideOut(canvas, name, side, delay);

        yield return Background(k => ShaderBackground.Heat = 1f - k);

        SynopsisTransition.SetBackgroundPaused(intro, false); // renders it now
        _cover = null;
        Destroy(canvas.gameObject); // before this frame is drawn
    }

    // -------------------- SHARED --------------------

    // Runs set(0..1) over OutTime, then waits a frame so the last step is drawn.
    private static IEnumerator Background(Action<float> set)
    {
        for (float t = 0f; t < OutTime; t += Time.deltaTime)
        {
            set(Mathf.SmoothStep(0f, 1f, t / OutTime));
            yield return null;
        }
        set(1f);
        yield return null;
    }

    private static void SlideOut(Transform canvas, string name, Vector2 side, float delay)
    {
        Transform t = Find(canvas, name);
        if (t == null || !t.gameObject.activeInHierarchy) return;

        LeanTween.cancel(t.gameObject); // e.g. Brainy mid-hop
        // Brainy's idle float would pull it back on screen once the tween ends.
        if (t.TryGetComponent(out BrainBehavior brain))
        {
            brain.StopAllCoroutines();
            brain.enabled = false;
        }
        LeanTween.moveLocal(t.gameObject, t.localPosition + Offscreen(t, side), SlideTime)
            .setDelay(delay).setEaseInCubic();
    }

    // Puts an element just off screen on `side` and slides it back to where it is now.
    public static void SlideIn(Transform t, Vector2 side, float delay)
    {
        Vector3 rest = t.localPosition;
        LeanTween.cancel(t.gameObject);
        t.localPosition = rest + Offscreen(t, side);
        LeanTween.moveLocal(t.gameObject, rest, SlideTime).setDelay(delay).setEaseOutCubic();
    }

    private static Transform RootCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponent<Canvas>() != null) return root.transform;
        return null;
    }

    // ThemedTextBoxes swaps a scroll view for a panel named after it.
    private static Transform Find(Transform canvas, string name)
    {
        return canvas.Find(name + "Panel") ?? canvas.Find(name);
    }

    private static Vector3 Offscreen(Transform t, Vector2 side)
    {
        return Vector2.Scale(side, ((RectTransform)t.parent).rect.size);
    }

    // A clear full-screen layer on top of the canvas that catches every click.
    // The button just pressed is let go too, so Enter can't press it again.
    public static GameObject BlockClicks(Transform canvas)
    {
        EventSystem.current?.SetSelectedGameObject(null);
        var blocker = new GameObject("TransitionClickBlocker", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)blocker.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = Color.clear;
        blocker.GetComponent<CanvasRenderer>().cullTransparentMesh = true; // blocks clicks without being drawn
        return blocker;
    }
}
