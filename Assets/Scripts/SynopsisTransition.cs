using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.SynopsisTransition (Group 2): the intro screen ("synopsis") has the
// response screen's shader background turned up (ShaderBackground.Excite),
// and Continue turns the one screen into the other in 3 seconds, with no fade
// and no scene change to stall it:
//  0. Whatever would open the intro opens GameScene instead (SynopsisScene),
//     which lays IntroductionScene over itself, unchanged, as the screen
//     fades in from black. GameScene sets itself up underneath, but holds
//     back the round itself (Holding / Revealed): the scenario's reveal, the
//     deal, and the logged start of the hand. The click log calls it all
//     "introduction" until then.
//  1. Out: the speech bubble and Continue fade away; the background calms
//     into the response screen's look while the NPC moves and shrinks to
//     where the response screen's avatar stands, taking on its pose as they
//     arrive.
//  2. The intro comes off. The response screen below matches its last frame
//     exactly (same background, same avatar), so nothing seems to change.
//  3. In: the scenario drops in from the top, the response box and Brainy
//     slide in from the left, Submit from the right, the buttons from below,
//     and the hand is dealt. Clicks wait until it's done.
public class SynopsisTransition : MonoBehaviour
{
    private const float OutTime = 1.8f;
    private const float FadeTime = 0.3f;     // bubble and Continue
    private const float MoveDelay = 0.15f;   // the NPC sets off once the bubble's going
    private const float PoseTime = 0.4f;     // the NPC's change of pose, at the end of the move
    private const float InTime = 1.2f;
    private const float SlideTime = 0.65f;
    private const float CardsDelay = 0.3f;
    private const int IntroSortingOrder = 100; // above the response screen, below SceneTransition's fade

    private static bool _pending;            // GameScene was asked for with its synopsis
    private static SynopsisTransition _game; // GameScene's, while its synopsis is showing
    private static bool _leaving;

    public static bool Enabled => TestGroups.IsEnabled(Feature.SynopsisTransition);

    // True while the synopsis covers the response screen: GameManager and
    // CardBehavior hold the round back until Revealed.
    public static bool Holding => _game != null;
    public static event Action Revealed;

    // LastCards as it was before the response screen dealt, for the intro's
    // request to the worker: the intro used to load (and ask) first.
    public static string CardsBeforeDeal { get; private set; }

    // The scene to load to show the synopsis: IntroductionScene, or with
    // this feature GameScene, which lays the intro over itself.
    public static string SynopsisScene()
    {
        if (!Enabled) return "IntroductionScene";
        _pending = true;
        return "GameScene";
    }

    // Whether this scene is (for now) showing the synopsis, so the click log
    // can call it "introduction".
    public static bool ShowsSynopsis(Scene scene)
    {
        return scene.name == "GameScene" && (_pending || _game != null);
    }

    // Elements that slide in, the side they come from, and when they set off.
    private static readonly (string name, Vector2 from, float delay)[] SlideIns =
    {
        ("Bubble Box", Vector2.up, 0f),
        ("ResponseInput", Vector2.left, 0.1f),
        ("Brain", Vector2.left, 0.18f),
        ("SubmitButton", Vector2.right, 0.22f),
        ("BackButton", Vector2.down, 0.4f),
        ("RefreshCardsButton", Vector2.down, 0.45f),
    };

    private Transform[] _slides;
    private Vector3[] _restPositions;
    private Vector3[] _restScales;
    private RectTransform _avatar;

    // -------------------- SETUP --------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "GameScene" && mode == LoadSceneMode.Single && _pending)
            {
                _pending = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<Canvas>() == null) continue;
                    _game = root.AddComponent<SynopsisTransition>();
                    break;
                }
                if (_game == null) return;

                Revealed = null; // anything left from a screen that never got revealed
                CardsBeforeDeal = PlayerPrefs.GetString("LastCards", "");
                SceneManager.LoadScene("IntroductionScene", LoadSceneMode.Additive);
            }
            else if (scene.name == "IntroductionScene" && mode == LoadSceneMode.Additive)
            {
                LayOver(scene);
            }
        };
    }

    // The intro, drawn over the response screen. Its own camera and event
    // system stand down; the response screen's do the work, and the response
    // screen's background, hidden beneath, pauses until the reveal.
    private static void LayOver(Scene intro)
    {
        if (_game != null) SetBackgroundPaused(_game.gameObject.scene, true);

        foreach (GameObject root in intro.GetRootGameObjects())
        {
            if (root.TryGetComponent(out Canvas canvas))
                canvas.sortingOrder = IntroSortingOrder;
            else if (root.GetComponent<Camera>() != null || root.GetComponent<EventSystem>() != null)
                root.SetActive(false);
        }
    }

    private static void SetBackgroundPaused(Scene scene, bool paused)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (ShaderBackground background in root.GetComponentsInChildren<ShaderBackground>())
                background.SetPaused(paused);
    }

    private void Awake()
    {
        _avatar = transform.Find("Avatar") as RectTransform;
        _slides = new Transform[SlideIns.Length];
        for (int i = 0; i < SlideIns.Length; i++) _slides[i] = transform.Find(SlideIns[i].name);
    }

    // Resting spots, taken once everything's laid out (the other scripts'
    // Awake, sceneLoaded and Start), while the synopsis still covers it all.
    private void LateUpdate()
    {
        if (_restPositions != null) return;
        _restPositions = new Vector3[_slides.Length];
        _restScales = new Vector3[_slides.Length];
        for (int i = 0; i < _slides.Length; i++)
        {
            if (_slides[i] == null) continue;
            _restPositions[i] = _slides[i].localPosition;
            _restScales[i] = _slides[i].localScale;
        }
    }

    // -------------------- OUT (the intro, over GameScene) --------------------

    public static IEnumerator Leave(RectTransform npc, GameObject bubble, GameObject continueButton)
    {
        if (_game == null)
        {
            SceneTransition.Load("GameScene"); // not laid over a response screen: just go there
            yield break;
        }
        if (_leaving) yield break;
        _leaving = true;

        CanvasGroup bubbleGroup = FadeGroup(bubble);
        CanvasGroup buttonGroup = FadeGroup(continueButton);
        Vector3 bubbleFrom = bubble.transform.localPosition;
        Vector3 buttonFrom = continueButton.transform.localPosition;

        // Both canvases share a reference resolution, so the response screen's
        // avatar can be matched value for value.
        RectTransform target = _game._avatar;
        LeanTween.cancel(npc.gameObject);
        Vector2 anchorFrom = npc.anchorMin;
        Vector2 positionFrom = npc.anchoredPosition;
        Vector2 sizeFrom = npc.sizeDelta;
        var npcImage = npc.GetComponent<Image>();
        Image pose = target != null ? AddPose(npc, target.GetComponent<Image>()) : null;

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float time = Mathf.Min(t, OutTime);

            float fade = Mathf.SmoothStep(0f, 1f, time / FadeTime);
            bubbleGroup.alpha = buttonGroup.alpha = 1f - fade;
            bubble.transform.localPosition = bubbleFrom + Vector3.up * 16f * fade;
            continueButton.transform.localPosition = buttonFrom + Vector3.up * 16f * fade;

            ShaderBackground.Excite = 1f - Mathf.SmoothStep(0f, 1f, time / OutTime);

            if (target != null)
            {
                float move = EaseInOutCubic(Mathf.Clamp01((time - MoveDelay) / (OutTime - MoveDelay)));
                npc.anchorMin = npc.anchorMax = Vector2.LerpUnclamped(anchorFrom, target.anchorMin, move);
                npc.anchoredPosition = Vector2.LerpUnclamped(positionFrom, target.anchoredPosition, move);
                npc.sizeDelta = Vector2.LerpUnclamped(sizeFrom, target.sizeDelta, move);
            }

            if (pose != null)
            {
                float swap = Mathf.SmoothStep(0f, 1f, (time - (OutTime - PoseTime)) / PoseTime);
                SetAlpha(pose, swap);
                SetAlpha(npcImage, 1f - swap);
            }

            if (t >= OutTime) break;
            yield return null;
        }

        // The finished state has been drawn; the response screen below
        // matches it exactly.
        yield return null;
        _leaving = false;
        if (_game != null) _game.Reveal(npc.gameObject.scene);
    }

    private static CanvasGroup FadeGroup(GameObject go)
    {
        if (!go.TryGetComponent(out CanvasGroup group)) group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        return group;
    }

    // The response screen's pose, laid over the NPC, hidden until the end of the move.
    private static Image AddPose(RectTransform npc, Image target)
    {
        if (target == null || target.sprite == null) return null;

        var go = new GameObject("ArrivingPose", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(npc, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = target.sprite;
        image.raycastTarget = false;
        SetAlpha(image, 0f);
        return image;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    private static float EaseInOutCubic(float x)
    {
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    // -------------------- IN (GameScene) --------------------

    // All in one frame: the intro comes off and everything that slides in
    // jumps off screen, so the first frame without the intro looks like its last.
    private void Reveal(Scene intro)
    {
        // The response screen's background takes over from the intro's on
        // this very frame. The intro goes idle now and is unloaded once the
        // slide-ins are done, so unloading it doesn't compete with them.
        SetBackgroundPaused(gameObject.scene, false);
        SetBackgroundPaused(intro, true);
        foreach (GameObject root in intro.GetRootGameObjects())
            if (root.TryGetComponent(out Canvas canvas)) canvas.enabled = false;
        LeanTween.delayedCall(InTime, () => SceneManager.UnloadSceneAsync(intro));

        _game = null;
        CardsBeforeDeal = null;
        ClickLogger.SwitchScreen("GameScene");

        var blocker = new GameObject("TransitionClickBlocker", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)blocker.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        blocker.GetComponent<Image>().color = Color.clear;
        Destroy(blocker, InTime);

        Vector2 canvasSize = ((RectTransform)transform).rect.size;
        for (int i = 0; i < _slides.Length; i++)
        {
            if (_slides[i] == null) continue;
            GameObject go = _slides[i].gameObject;
            LeanTween.cancel(go); // e.g. Brainy mid-hop
            go.transform.localScale = _restScales[i];
            go.transform.localPosition = _restPositions[i] + (Vector3)Vector2.Scale(SlideIns[i].from, canvasSize);
            LeanTween.moveLocal(go, _restPositions[i], SlideTime).setDelay(SlideIns[i].delay).setEaseOutCubic();
        }

        var cards = FindObjectsByType<CardBehavior>(FindObjectsSortMode.None);
        Array.Sort(cards, (a, b) => string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < cards.Length; i++) cards[i].DealIn(CardsDelay + i * CardBehavior.DealStagger);

        Action revealed = Revealed;
        Revealed = null;
        revealed?.Invoke();

        Destroy(this);
    }
}
