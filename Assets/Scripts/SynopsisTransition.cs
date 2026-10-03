using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.SynopsisTransition (Group 2): the intro screen ("synopsis") has the
// response screen's shader background turned up (ShaderBackground.Excite),
// and Continue turns the one screen into the other in 3 seconds, with no fade:
//  1. Out (in IntroductionScene): the speech bubble and Continue fade away;
//     the background calms into the response screen's look while the NPC
//     moves and shrinks to where the response screen's avatar stands, taking
//     on its pose as they arrive.
//  2. GameScene loads with no fade. Its background and avatar look exactly
//     like the intro's last frame, so the switch can't be seen.
//  3. In (in GameScene): the scenario drops in from the top, the response box
//     and Brainy slide in from the left, Submit from the right, the buttons
//     from below, and the hand is dealt. Clicks wait until it's done.
public class SynopsisTransition : MonoBehaviour
{
    private const float OutTime = 1.8f;
    private const float FadeTime = 0.3f;     // bubble and Continue
    private const float MoveDelay = 0.15f;   // the NPC sets off once the bubble's going
    private const float PoseTime = 0.4f;     // the NPC's change of pose, at the end of the move
    private const float InTime = 1.2f;
    private const float SlideTime = 0.65f;
    private const float CardsDelay = 0.3f;

    // GameScene's Avatar: where it stands and its sprite (from GameScene.unity).
    private static readonly Vector2 AvatarAnchor = new Vector2(0.842898f, 0.43777782f);
    private static readonly Vector2 AvatarPosition = new Vector2(1.25f, -0.26799774f);
    private static readonly Vector2 AvatarSize = new Vector2(83.4162f, 207.2626f);
    private const string AvatarSprite = "AvatarHandRaiseEyesClosed";

    private static bool _leaving;
    private static bool _arriving;

    public static bool Enabled => TestGroups.IsEnabled(Feature.SynopsisTransition);

    // How long CardBehavior holds the hand back: through the other slide-ins
    // when arriving from the intro.
    public static float DealDelay => _arriving ? CardsDelay : 0f;

    private CanvasGroup[] _hidden;
    private GameObject _blocker;

    // -------------------- OUT (IntroductionScene) --------------------

    public static IEnumerator Leave(RectTransform npc, GameObject bubble, GameObject continueButton)
    {
        if (_leaving) yield break;
        _leaving = true;

        CanvasGroup bubbleGroup = FadeGroup(bubble);
        CanvasGroup buttonGroup = FadeGroup(continueButton);
        Vector3 bubbleFrom = bubble.transform.localPosition;
        Vector3 buttonFrom = continueButton.transform.localPosition;

        LeanTween.cancel(npc.gameObject);
        Vector2 anchorFrom = npc.anchorMin;
        Vector2 positionFrom = npc.anchoredPosition;
        Vector2 sizeFrom = npc.sizeDelta;
        var npcImage = npc.GetComponent<Image>();
        Image pose = AddPose(npc);

        // Loaded while this plays: loading it at the end froze the screen
        // for about a second.
        AsyncOperation load = SceneTransition.PreloadWithoutFade("GameScene");

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float time = Mathf.Min(t, OutTime);

            float fade = Mathf.SmoothStep(0f, 1f, time / FadeTime);
            bubbleGroup.alpha = buttonGroup.alpha = 1f - fade;
            bubble.transform.localPosition = bubbleFrom + Vector3.up * 16f * fade;
            continueButton.transform.localPosition = buttonFrom + Vector3.up * 16f * fade;

            ShaderBackground.Excite = 1f - Mathf.SmoothStep(0f, 1f, time / OutTime);

            float move = EaseInOutCubic(Mathf.Clamp01((time - MoveDelay) / (OutTime - MoveDelay)));
            npc.anchorMin = npc.anchorMax = Vector2.LerpUnclamped(anchorFrom, AvatarAnchor, move);
            npc.anchoredPosition = Vector2.LerpUnclamped(positionFrom, AvatarPosition, move);
            npc.sizeDelta = Vector2.LerpUnclamped(sizeFrom, AvatarSize, move);

            if (pose != null)
            {
                float swap = Mathf.SmoothStep(0f, 1f, (time - (OutTime - PoseTime)) / PoseTime);
                SetAlpha(pose, swap);
                SetAlpha(npcImage, 1f - swap);
            }

            if (t >= OutTime) break;
            yield return null;
        }

        // The finished state has been drawn; the response screen picks up
        // from exactly here.
        yield return null;
        _leaving = false;
        _arriving = true;
        load.allowSceneActivation = true;
    }

    private static CanvasGroup FadeGroup(GameObject go)
    {
        if (!go.TryGetComponent(out CanvasGroup group)) group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        return group;
    }

    // The response screen's pose, laid over the NPC, hidden until the end of the move.
    private static Image AddPose(RectTransform npc)
    {
        var sprite = Resources.Load<Sprite>(AvatarSprite);
        if (sprite == null) return null;

        var go = new GameObject("ArrivingPose", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(npc, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            _leaving = false;
            if (scene.name == "IntroductionScene") WarmUp(scene);
            if (!_arriving) return;

            if (scene.name == "GameScene")
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<Canvas>() == null) continue;
                    root.AddComponent<SynopsisTransition>();
                    return;
                }
            }
            _arriving = false;
        };
    }

    // The textures the response screen brings in: the card art, the pixel
    // buttons, its background and Brainy. Taken in on its first frame, they
    // held the screen still for up to a second just before the slide-ins.
    // Instead they're loaded as the intro opens (still behind its fade from
    // black), drawn once there, invisibly, so the GPU really has them, and
    // kept for the session.
    private static List<Texture> _warm;

    private static void WarmUp(Scene intro)
    {
        if (_warm != null) return;
        _warm = new List<Texture>();

        CardArt art = CardArt.Load();
        if (art != null && art.areas != null)
        {
            foreach (CardArt.AreaArt area in art.areas)
            {
                if (area == null) continue;
                foreach (CardArt.Layer[] side in new[] { area.backLayers, area.frontLayers })
                {
                    if (side == null) continue;
                    foreach (CardArt.Layer layer in side)
                        if (layer != null && layer.sprite != null) _warm.Add(layer.sprite.texture);
                }
            }
        }
        foreach (Sprite s in Resources.LoadAll<Sprite>(PixelButton.Folder.TrimEnd('/'))) _warm.Add(s.texture);
        foreach (string name in new[] { "GameBG", "brainy", AvatarSprite })
        {
            var t = Resources.Load<Texture2D>(name);
            if (t != null) _warm.Add(t);
        }
        _warm.Add(PanelSprites.Pill.texture);

        foreach (GameObject root in intro.GetRootGameObjects())
        {
            if (root.GetComponent<Canvas>() == null) continue;
            foreach (Texture t in new HashSet<Texture>(_warm))
            {
                var go = new GameObject("WarmUp", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(root.transform, false);
                ((RectTransform)go.transform).sizeDelta = new Vector2(2f, 2f);
                var image = go.GetComponent<RawImage>();
                image.texture = t;
                image.color = Color.clear; // still drawn, just not seen
                image.raycastTarget = false;
                Destroy(go, 0.5f);
            }
            return;
        }
    }

    // Hidden from the first frame, then moved in LateUpdate, once the other
    // scripts' Awake, sceneLoaded and Start have laid everything out.
    private void Awake()
    {
        _hidden = new CanvasGroup[SlideIns.Length];
        for (int i = 0; i < SlideIns.Length; i++)
        {
            Transform t = transform.Find(SlideIns[i].name);
            if (t == null) continue;
            if (!t.TryGetComponent(out _hidden[i])) _hidden[i] = t.gameObject.AddComponent<CanvasGroup>();
            _hidden[i].alpha = 0f;
        }

        _blocker = new GameObject("TransitionClickBlocker", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)_blocker.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _blocker.GetComponent<Image>().color = Color.clear;
    }

    private void LateUpdate()
    {
        if (!_arriving) return;
        _arriving = false;

        Vector2 canvasSize = ((RectTransform)transform).rect.size;
        _blocker.transform.SetAsLastSibling();

        for (int i = 0; i < SlideIns.Length; i++)
        {
            if (_hidden[i] == null) continue;
            _hidden[i].alpha = 1f;

            GameObject go = _hidden[i].gameObject;
            Vector3 rest = go.transform.localPosition;
            go.transform.localPosition = rest + (Vector3)Vector2.Scale(SlideIns[i].from, canvasSize);
            LeanTween.moveLocal(go, rest, SlideTime).setDelay(SlideIns[i].delay).setEaseOutCubic();
        }

        Destroy(_blocker, InTime);
        Destroy(this, InTime);
    }
}
