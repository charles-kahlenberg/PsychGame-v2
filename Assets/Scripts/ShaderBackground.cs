using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.ShaderBackground (Group 2): the response screen (GameScene) swaps
// the therapy-room image for Resources/ThoughtCurrents.shader, a slow flowing
// layered pattern. It animates on its own, so BackgroundDrift leaves this
// scene alone (see Replaces).
//
// Like BackgroundDrift, the shader is drawn on a new bottom child of the
// Canvas. The Canvas's own Image stays as an invisible click target so clicks
// on empty space still hit (and are logged as) the same object as in Group 1.
public class ShaderBackground : MonoBehaviour
{
    private const string SceneName = "GameScene";
    private const string ShaderResource = "ThoughtCurrents";
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    private RectTransform _rt;
    private Material _material;

    public static bool Replaces(Scene scene)
    {
        return scene.name == SceneName && TestGroups.IsEnabled(Feature.ShaderBackground);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.ShaderBackground)) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (Replaces(scene)) AddTo(scene);
        };
    }

    private static void AddTo(Scene scene)
    {
        var shader = Resources.Load<Shader>(ShaderResource);
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning($"[ShaderBackground] {ShaderResource} isn't available here; keeping the image background.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;

            var canvasImage = canvas.GetComponent<Image>();
            if (canvasImage == null || canvasImage.sprite == null) continue;

            CreateUnder(canvasImage, shader);
        }
    }

    private static void CreateUnder(Image source, Shader shader)
    {
        var go = new GameObject("ShaderBackground", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(source.transform, false);
        go.transform.SetAsFirstSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var material = new Material(shader) { name = "ThoughtCurrents (runtime)" };
        var image = go.GetComponent<Image>();
        image.material = material;
        image.raycastTarget = false;

        Color c = source.color;
        c.a = 0f;
        source.color = c;

        go.AddComponent<ShaderBackground>()._material = material;
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
    }

    // Keeps the pattern's proportions right however the window is shaped.
    private void Update()
    {
        Rect rect = _rt.rect;
        if (_material != null && rect.height > 0f)
            _material.SetFloat(AspectId, rect.width / rect.height);
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
