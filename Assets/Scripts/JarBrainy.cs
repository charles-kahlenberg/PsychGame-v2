using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Group 2 (Feature.JarBrainy): every Brainy is the jar, Resources/poirot.png,
// with the moustache, Resources/moustache.png, laid on separately so it can
// twitch now and then. Feature.JarAvatar does the same for the player's
// avatar: Brainette's jar, Resources/girlbrainy.png, with her bow,
// Resources/girlbrainybow.png. Apply / ApplyAvatar set an image up; this
// component sits on the moustache or bow and twitches it.
public class JarBrainy : MonoBehaviour
{
    // A jar and the piece laid over it: where that sits in the jar's art
    // (0-1 across and up), its hinge (0-1 across and up the piece) and how
    // much bigger than in the art it's drawn, around the hinge.
    private struct Look
    {
        public string jar, piece;
        public Vector2 min, max, pivot;
        public float size;
    }

    private static readonly Look Brainy = new Look
    {
        jar = "poirot", piece = "moustache",
        min = new Vector2(0.3543f, 0.3609f), max = new Vector2(0.8346f, 0.4887f),
        pivot = new Vector2(0.5f, 0.7f), // hinged just under the nose
        size = 1.2f,
    };

    // girlbrainy.png is framed like poirot.png. The bow sits on the upper
    // right of the jar, just under the lid, a bit smaller than drawn so it
    // clears her eye.
    private static readonly Look Girl = new Look
    {
        jar = "girlbrainy", piece = "girlbrainybow",
        min = new Vector2(0.5984f, 0.5451f), max = new Vector2(0.9843f, 0.8684f),
        pivot = new Vector2(0.5f, 0.5f),
        size = 0.8f,
    };

    // The avatar's box was drawn for a standing person; the jar is this share
    // of its height, at the same spot across but centred up the screen.
    private const float AvatarJarHeight = 0.55f;

    private const float Tilt = 7f;     // degrees each flick lifts one side
    private const float Flick = 0.12f; // seconds per flick

    // stretch: the jar fills the image's box instead of keeping its shape
    // (the response screen draws Brainy a touch wider than the art).
    public static bool Apply(GameObject brainy, bool stretch = false) => Apply(brainy, Brainy, stretch);

    // The player's avatar (the synopsis's NPC, the response screen's Avatar).
    // Call before the scene's layout measures it (Awake).
    public static bool ApplyAvatar(GameObject avatar)
    {
        if (!Apply(avatar, Girl, false)) return false;

        var rt = (RectTransform)avatar.transform;
        var jar = avatar.GetComponent<Image>().sprite;
        float height = rt.sizeDelta.y * AvatarJarHeight;
        rt.sizeDelta = new Vector2(height * jar.rect.width / jar.rect.height, height);
        rt.anchorMin = rt.anchorMax = new Vector2(rt.anchorMin.x, 0.5f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, (rt.pivot.y - 0.5f) * height);
        return true;
    }

    private static bool Apply(GameObject target, Look look, bool stretch)
    {
        var image = target != null ? target.GetComponent<Image>() : null;
        var jar = Resources.Load<Sprite>(look.jar);
        var piece = Resources.Load<Sprite>(look.piece);
        if (image == null || jar == null || piece == null)
        {
            Debug.LogWarning($"[JarBrainy] The image, Resources/{look.jar} or Resources/{look.piece} not found; keeping the old art.");
            return false;
        }
        image.sprite = jar;
        image.preserveAspect = !stretch;

        // Covers just the drawn jar, so the piece stays in place whatever
        // shape the image's box is.
        var frame = (RectTransform)new GameObject(look.piece + "Frame", typeof(RectTransform)).transform;
        frame.SetParent(target.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        if (!stretch)
        {
            var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = jar.rect.width / jar.rect.height;
        }

        var rt = (RectTransform)new GameObject(look.piece, typeof(RectTransform), typeof(Image), typeof(JarBrainy)).transform;
        rt.SetParent(frame, false);
        Vector2 hinge = look.min + Vector2.Scale(look.max - look.min, look.pivot);
        rt.anchorMin = hinge + (look.min - hinge) * look.size;
        rt.anchorMax = hinge + (look.max - hinge) * look.size;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = look.pivot;
        var pieceImage = rt.GetComponent<Image>();
        pieceImage.sprite = piece;
        pieceImage.raycastTarget = false;
        return true;
    }

    // Coroutines stop while Brainy is hidden (the rules screen hides it at
    // first), so the twitching starts again whenever it's shown.
    void OnEnable() => StartCoroutine(TwitchLoop());

    void OnDisable() => Settle();

    // The inquisitive cartoon twitch: every few seconds one side hitches up
    // two or three times in quick succession, then it settles back in place.
    IEnumerator TwitchLoop()
    {
        var rt = (RectTransform)transform;
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(1.5f, 4f));
            float tilt = Random.value < 0.5f ? -Tilt : Tilt;
            int flicks = Random.Range(2, 4);
            for (int i = 0; i < flicks; i++)
            {
                for (float t = 0f; t < Flick; t += Time.deltaTime)
                {
                    float k = Mathf.Sin(t / Flick * Mathf.PI); // 0 -> 1 -> 0
                    rt.localRotation = Quaternion.Euler(0f, 0f, tilt * k);
                    rt.localScale = new Vector3(1f, 1f + 0.12f * k, 1f);
                    rt.anchoredPosition = new Vector2(0f, rt.rect.height * 0.08f * k);
                    yield return null;
                }
            }
            Settle();
        }
    }

    void Settle()
    {
        var rt = (RectTransform)transform;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }
}
