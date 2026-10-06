using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Group 2 (Feature.JarBrainy): every Brainy is the jar, Resources/poirot.png,
// with the moustache, Resources/moustache.png, laid on separately so it can
// twitch now and then. Apply sets a Brainy image up; this component sits on
// the moustache and twitches it.
public class JarBrainy : MonoBehaviour
{
    // Where the moustache sits in poirot.png.
    private static readonly Vector2 MoustacheMin = new Vector2(0.3543f, 0.3609f);
    private static readonly Vector2 MoustacheMax = new Vector2(0.8346f, 0.4887f);
    private const float Tilt = 7f;     // degrees each flick lifts one side
    private const float Flick = 0.12f; // seconds per flick

    // stretch: the jar fills the image's box instead of keeping its shape
    // (the response screen draws Brainy a touch wider than the art).
    public static bool Apply(GameObject brainy, bool stretch = false)
    {
        var image = brainy != null ? brainy.GetComponent<Image>() : null;
        var jar = Resources.Load<Sprite>("poirot");
        var moustache = Resources.Load<Sprite>("moustache");
        if (image == null || jar == null || moustache == null)
        {
            Debug.LogWarning("[JarBrainy] Brainy's image, Resources/poirot or Resources/moustache not found; keeping the old Brainy.");
            return false;
        }
        image.sprite = jar;
        image.preserveAspect = !stretch;

        // Covers just the drawn jar, so the moustache stays on the face
        // whatever shape the image's box is.
        var frame = (RectTransform)new GameObject("MoustacheFrame", typeof(RectTransform)).transform;
        frame.SetParent(brainy.transform, false);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        if (!stretch)
        {
            var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = jar.rect.width / jar.rect.height;
        }

        var rt = (RectTransform)new GameObject("Moustache", typeof(RectTransform), typeof(Image), typeof(JarBrainy)).transform;
        rt.SetParent(frame, false);
        rt.anchorMin = MoustacheMin;
        rt.anchorMax = MoustacheMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.7f); // hinged just under the nose
        var moustacheImage = rt.GetComponent<Image>();
        moustacheImage.sprite = moustache;
        moustacheImage.raycastTarget = false;
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
