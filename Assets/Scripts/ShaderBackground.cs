using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.ShaderBackground (Group 2): the response screen (GameScene) swaps
// the therapy-room image for Resources/ThoughtCurrents.shader, a slow flowing
// layered pattern. The grading and review screens, which were a flat camera
// colour, get it too. It animates on its own, so BackgroundDrift leaves these
// scenes alone (see Replaces).
//
// The noise behind the pattern is expensive, so it isn't run per screen
// pixel: every frame this renders it into a small texture with
// ThoughtCurrentsField.shader, and ThoughtCurrents.shader samples that at
// full resolution to draw the crisp layers and contours on top.
//
// With Feature.SynopsisTransition the intro screen gets it too, turned up
// (Excite = 1): SynopsisTransition dials that down to the response screen's
// calm look when the player moves on. The flow is one clock shared by every
// screen, so the pattern carries on where the last screen left it.
//
// Like BackgroundDrift, the shader is drawn on a new bottom child of the
// Canvas. Where the Canvas has its own Image (GameScene), that stays as an
// invisible click target so clicks on empty space still hit (and are logged
// as) the same object as in Group 1.
public class ShaderBackground : MonoBehaviour
{
    private static readonly string[] SceneNames = { "GameScene", "GradingScene", "ReviewScene" };
    private const string IntroSceneName = "IntroductionScene";
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
    private static readonly int ExciteId = Shader.PropertyToID("_Excite");
    private static readonly int ExciteScaleId = Shader.PropertyToID("_ExciteScale");
    private static readonly int ExciteWarpId = Shader.PropertyToID("_ExciteWarp");
    private static readonly int ExciteFlowSpeedId = Shader.PropertyToID("_ExciteFlowSpeed");

    // 0 is the calm look, 1 the intro's lively one; 1 on the intro screen and
    // 0 everywhere else as each loads.
    public static float Excite;

    private static Shader _shader, _fieldShader;
    private static float _flowTime;
    private static float _flowSpeed = -1f; // -1 until the first frame
    private static int _flowFrame = -1;
    private const float FlowSpeedLag = 0.8f; // seconds

    private RectTransform _rt;
    private Material _material;
    private bool _paused;
    private Material _fieldMaterial;
    private RenderTexture _field;

    public static bool Replaces(Scene scene)
    {
        if (!TestGroups.IsEnabled(Feature.ShaderBackground)) return false;
        return System.Array.IndexOf(SceneNames, scene.name) >= 0 ||
               (scene.name == IntroSceneName && TestGroups.IsEnabled(Feature.SynopsisTransition));
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
        // Kept for the whole session. Left to unload between scenes, they
        // had to be compiled again on the next screen's first frame, which
        // froze it for about a second (hidden by the fades, but not by
        // SynopsisTransition's switch).
        if (_shader == null) _shader = Resources.Load<Shader>(ShaderResource);
        if (_fieldShader == null) _fieldShader = Resources.Load<Shader>(FieldShaderResource);
        Shader shader = _shader, fieldShader = _fieldShader;
        if (shader == null || !shader.isSupported || fieldShader == null || !fieldShader.isSupported ||
            FieldFormat() == null)
        {
            Debug.LogWarning($"[ShaderBackground] {ShaderResource} isn't available here; keeping the image background.");
            return;
        }

        Excite = scene.name == IntroSceneName ? 1f : 0f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;

            CreateUnder(canvas, shader, fieldShader);
        }
    }

    private static void CreateUnder(Canvas canvas, Shader shader, Shader fieldShader)
    {
        var go = new GameObject("ShaderBackground", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
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

        // The old background (the Canvas's own Image, or the intro's plain
        // "Background" panel) stays as an invisible click target.
        Hide(canvas.GetComponent<Image>());
        Hide(canvas.transform.Find("Background")?.GetComponent<Image>());

        var background = go.AddComponent<ShaderBackground>();
        background._material = material;
        background._fieldMaterial = new Material(fieldShader) { name = "ThoughtCurrentsField (runtime)" };
    }

    private static void Hide(Image image)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = 0f;
        image.color = c;
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

    // While something opaque covers it (SynopsisTransition's intro over
    // GameScene) it's neither drawn nor rendered: the two backgrounds
    // together dropped frames. Unpausing renders straight away, so it's
    // up to date on the very frame it's shown.
    public void SetPaused(bool paused)
    {
        _paused = paused;
        GetComponent<Image>().enabled = !paused;
        if (!paused) Render();
    }

    private void Update()
    {
        if (!_paused) Render();
    }

    private void Render()
    {
        Rect rect = _rt.rect;
        if (_material == null || _fieldMaterial == null || rect.height <= 0f) return;

        float aspect = rect.width / rect.height;
        EnsureField(Mathf.Max(1, Mathf.RoundToInt(FieldHeight * aspect)));

        // The look is tuned on the main material; the field pass follows it,
        // between the calm and excited values.
        float e = Mathf.Clamp01(Excite);
        _material.SetFloat(ExciteId, e);
        _material.SetFloat(AspectId, aspect);
        _fieldMaterial.SetFloat(ScaleId, Mathf.Lerp(_material.GetFloat(ScaleId), _material.GetFloat(ExciteScaleId), e));
        _fieldMaterial.SetFloat(WarpId, Mathf.Lerp(_material.GetFloat(WarpId), _material.GetFloat(ExciteWarpId), e));
        _fieldMaterial.SetFloat(AspectId, aspect);

        // Advanced by speed rather than computed from the time, so changing
        // speed never jumps the pattern; once a frame, however many are drawn.
        // The speed trails Excite by about FlowSpeedLag: dropping with it, the
        // pattern all but stopped as the shapes finished calming, then crept
        // on again.
        if (_flowFrame != Time.frameCount)
        {
            _flowFrame = Time.frameCount;
            float target = Mathf.Lerp(_material.GetFloat(FlowSpeedId), _material.GetFloat(ExciteFlowSpeedId), e);
            _flowSpeed = _flowSpeed < 0f ? target : Mathf.Lerp(_flowSpeed, target, 1f - Mathf.Exp(-Time.deltaTime / FlowSpeedLag));
            _flowTime += Time.deltaTime * _flowSpeed;
        }
        _fieldMaterial.SetFloat(FlowTimeId, _flowTime);

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
