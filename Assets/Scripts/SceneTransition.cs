using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Every scene change in the game goes through SceneTransition.Load. Without
// Feature.ImprovedMenuTransitions (Group 1) it switches instantly, exactly as
// before. With it (Group 2) the screen fades out, the scene loads, and the
// new one fades in. Scenes loaded some other way (LoadingScreen's async load)
// still get the fade-in.
public class SceneTransition : MonoBehaviour
{
    private const float FadeOutTime = 0.25f;
    private const float FadeInTime = 0.35f;
    private static readonly Color FadeColor = new Color(0.04f, 0.04f, 0.07f);

    private static SceneTransition _instance;

    private CanvasGroup _canvasGroup;
    private Coroutine _fade;
    private bool _loading;

    public static void Load(string sceneName)
    {
        if (_instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }
        _instance.FadeOutAndLoad(sceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.ImprovedMenuTransitions)) return;

        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneTransition>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every scene's UI
        gameObject.AddComponent<GraphicRaycaster>();
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var overlay = new GameObject("SceneFade", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        var rt = (RectTransform)overlay.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = FadeColor;

        // Start covered so the first scene fades in too.
        SetAlpha(1f);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void FadeOutAndLoad(string sceneName)
    {
        if (_loading) return; // ignore double clicks while already leaving
        _loading = true;
        StartFade(1f, FadeOutTime, () => SceneManager.LoadScene(sceneName));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _loading = false;
        SetAlpha(1f);
        StartFade(0f, FadeInTime, null);
    }

    private void StartFade(float target, float duration, System.Action onDone)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Fade(target, duration, onDone));
    }

    private IEnumerator Fade(float target, float duration, System.Action onDone)
    {
        float start = _canvasGroup.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetAlpha(target);
        _fade = null;
        onDone?.Invoke();
    }

    // Blocks clicks whenever the overlay is visible at all, so nothing can be
    // pressed mid-transition.
    private void SetAlpha(float alpha)
    {
        _canvasGroup.alpha = alpha;
        _canvasGroup.blocksRaycasts = alpha > 0.01f;
    }
}
