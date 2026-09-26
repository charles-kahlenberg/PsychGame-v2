using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.ShaderBackground (Group 2): the response screen (GameScene) swaps
// the therapy-room image for Resources/ThoughtCurrents.shader, a slow flowing
// layered pattern. It animates on its own, so BackgroundDrift leaves this
// scene alone (see Replaces).
//
// The noise behind the pattern is expensive, so it isn't run per screen
// pixel: every frame this renders it into a small texture with
// ThoughtCurrentsField.shader, and ThoughtCurrents.shader samples that at
// full resolution to draw the crisp layers and contours on top.
//
// Like BackgroundDrift, the shader is drawn on a new bottom child of the
// Canvas. The Canvas's own Image stays as an invisible click target so clicks
// on empty space still hit (and are logged as) the same object as in Group 1.
public class ShaderBackground : MonoBehaviour
{
    private const string SceneName = "GameScene";
    private const string ShaderResource = "ThoughtCurrents";
    private const string FieldShaderResource = "ThoughtCurrentsField";

    // Height of the noise texture. The shapes are large and it's sampled
    // smoothly, so this looks the same as full resolution at a fraction of
    // the cost (1080p screens run the noise on ~1/9 of the pixels).
    private const int FieldHeight = 360;

    private static readonly int FieldTexId = Shader.PropertyToID("_FieldTex");
    private static readonly int ScaleId = Shader.PropertyToID("_Scale");
    private static readonly int WarpId = Shader.PropertyToID("_Warp");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");

    private RectTransform _rt;
    private Material _material;
    private Material _fieldMaterial;
    private RenderTexture _field;

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
        var fieldShader = Resources.Load<Shader>(FieldShaderResource);
        if (shader == null || !shader.isSupported || fieldShader == null || !fieldShader.isSupported ||
            FieldFormat() == null)
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

            CreateUnder(canvasImage, shader, fieldShader);
        }
    }

    private static void CreateUnder(Image source, Shader shader, Shader fieldShader)
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

        var background = go.AddComponent<ShaderBackground>();
        background._material = material;
        background._fieldMaterial = new Material(fieldShader) { name = "ThoughtCurrentsField (runtime)" };
    }

    // A one-channel half-float texture keeps the field smooth; 8 bits per
    // channel would band the contours. Null when neither can be rendered to.
    private static RenderTextureFormat? FieldFormat()
    {
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf)) return RenderTextureFormat.RHalf;
        if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
        return null;
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
    }

    private void Update()
    {
        Rect rect = _rt.rect;
        if (_material == null || _fieldMaterial == null || rect.height <= 0f) return;

        float aspect = rect.width / rect.height;
        EnsureField(Mathf.Max(1, Mathf.RoundToInt(FieldHeight * aspect)));

        // The look is tuned on the main material; the field pass follows it.
        _fieldMaterial.SetFloat(ScaleId, _material.GetFloat(ScaleId));
        _fieldMaterial.SetFloat(WarpId, _material.GetFloat(WarpId));
        _fieldMaterial.SetFloat(AspectId, aspect);
        _fieldMaterial.SetFloat(FlowTimeId, Time.timeSinceLevelLoad * _material.GetFloat(FlowSpeedId));

        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(Texture2D.blackTexture, _field, _fieldMaterial);
        RenderTexture.active = previous;
    }

    // (Re)makes the field texture when the window's shape changes, or if
    // the browser dropped it (e.g. a lost WebGL context).
    private void EnsureField(int width)
    {
        if (_field != null && _field.width == width && _field.IsCreated()) return;

        ReleaseField();
        _field = new RenderTexture(width, FieldHeight, 0, FieldFormat().Value, RenderTextureReadWrite.Linear)
        {
            name = "ThoughtCurrentsField",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
        };
        _field.Create();
        _material.SetTexture(FieldTexId, _field);
    }

    private void ReleaseField()
    {
        if (_field == null) return;
        _field.Release();
        Destroy(_field);
        _field = null;
    }

    private void OnDestroy()
    {
        ReleaseField();
        if (_material != null) Destroy(_material);
        if (_fieldMaterial != null) Destroy(_fieldMaterial);
    }
}
