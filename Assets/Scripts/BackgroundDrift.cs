using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.BackgroundTweening (Group 2): each scene's background image slowly
// drifts and zooms. It's found at runtime, so no scene needs editing:
//  - the first child of a Canvas, when it's a full-screen Image with a sprite
//    (Splash, SaveSelect, Loading, Rules, GameEnd), or
//  - the Canvas's own Image (GameScene). That one is copied onto a new
//    bottom child first, so the rest of the UI doesn't drift with it.
//    With Feature.ShaderBackground, GameScene's background is an animated
//    shader instead, which moves on its own, so it's skipped.
// Scenes whose background is just the camera color (Grading, Review) and
// plain color panels (Introduction) have nothing to drift.
public class BackgroundDrift : MonoBehaviour
{
    private const float Zoom = 1.08f;            // enlarged so drifting never shows an edge
    private const float Breathe = 0.01f;         // extra zoom in and out on top of that
    private const float DriftFraction = 0.025f;  // of the image size, each way
    private const float Period = 24f;            // seconds per full drift cycle

    private RectTransform _rt;
    private Vector2 _restPosition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.BackgroundTweening)) return;
        SceneManager.sceneLoaded += (scene, mode) => AddToBackgroundsIn(scene);
    }

    private static void AddToBackgroundsIn(Scene scene)
    {
        if (ShaderBackground.Replaces(scene)) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;

            Image background = FindBackground(canvas);
            if (background != null && background.GetComponent<BackgroundDrift>() == null)
                background.gameObject.AddComponent<BackgroundDrift>();
        }
    }

    private static Image FindBackground(Canvas canvas)
    {
        if (canvas.transform.childCount > 0)
        {
            Transform first = canvas.transform.GetChild(0);
            var image = first.GetComponent<Image>();
            if (image != null && image.sprite != null && first.GetComponent<Selectable>() == null &&
                IsFullScreen((RectTransform)first))
            {
                return image;
            }
        }

        var canvasImage = canvas.GetComponent<Image>();
        if (canvasImage != null && canvasImage.sprite != null && canvas.GetComponent<Selectable>() == null)
            return CopyOntoBottomChild(canvasImage);

        return null;
    }

    private static bool IsFullScreen(RectTransform rt)
    {
        return rt.anchorMin.sqrMagnitude < 0.0001f && (rt.anchorMax - Vector2.one).sqrMagnitude < 0.0001f;
    }

    // Draws the canvas's background on a new child instead. The original stays
    // as an invisible click target so clicks on empty space still hit (and are
    // logged as) the same object as in Group 1.
    private static Image CopyOntoBottomChild(Image source)
    {
        var go = new GameObject("DriftingBackground", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(source.transform, false);
        go.transform.SetAsFirstSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var copy = go.GetComponent<Image>();
        copy.sprite = source.sprite;
        copy.type = source.type;
        copy.preserveAspect = source.preserveAspect;
        copy.color = source.color;
        copy.material = source.material;
        copy.raycastTarget = false;

        Color c = source.color;
        c.a = 0f;
        source.color = c;

        return copy;
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;

        // Zoom from the middle, whatever pivot the scene gave it.
        Vector2 offsetMin = _rt.offsetMin, offsetMax = _rt.offsetMax;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.offsetMin = offsetMin;
        _rt.offsetMax = offsetMax;

        _restPosition = _rt.anchoredPosition;
    }

    private void Update()
    {
        float t = Time.time * 2f * Mathf.PI / Period;
        Vector2 size = _rt.rect.size;

        _rt.anchoredPosition = _restPosition + new Vector2(
            Mathf.Sin(t) * size.x * DriftFraction,
            Mathf.Sin(t * 0.7f + 1f) * size.y * DriftFraction);

        float scale = Zoom + Mathf.Sin(t * 0.5f) * Breathe;
        _rt.localScale = new Vector3(scale, scale, 1f);
    }
}
