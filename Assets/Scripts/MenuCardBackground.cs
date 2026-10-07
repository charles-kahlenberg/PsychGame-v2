using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.MenuCardBackground (Group 2): the title screen's (SplashScene) room
// wall becomes Resources/MenuCards.shader, columns of card backs scrolling up
// and down. It's drawn on the wall's own Image (Canvas > Panel), so it stays
// the click target it was in Group 1. Everything else but the buttons is
// hidden, and Resources/MenuTitle goes on top above them.
// It moves on its own, so BackgroundDrift leaves this scene alone.
//
// The shader also lights the middle of the screen, and the title and
// buttons cast drop shadows onto the cards. This component, on the wall,
// tells it where the buttons are every frame, so their shadows follow them
// (e.g. as they pop on hover).
public class MenuCardBackground : MonoBehaviour
{
    private const string SceneName = "SplashScene";
    private const string ShaderResource = "MenuCards";
    private const string AtlasResource = "MenuCardBacks";
    private const string TitleResource = "MenuTitle";

    // Where the title sits, in the canvas's 800x450 reference units: centred
    // across, just above the top button, and clear of the top edge.
    private const float TitleHeight = 96f;
    private const float TitleY = 165f; // its centre, above the screen's centre
    private static readonly int CardTexId = Shader.PropertyToID("_CardTex");
    private static readonly int TitleTexId = Shader.PropertyToID("_TitleTex");
    private static readonly int TitleRectId = Shader.PropertyToID("_TitleRect");
    private static readonly int ButtonsId = Shader.PropertyToID("_Buttons");
    private static readonly int ButtonCountId = Shader.PropertyToID("_ButtonCount");

    private const int MaxButtons = 8; // the shader's _Buttons array
    // The visible button within each button image (424 x 112 of 445 x 133).
    private const float VisibleWidth = 0.953f, VisibleHeight = 0.84f;
    private static readonly Vector3[] Corners = new Vector3[4];

    private Material _material;
    private RectTransform[] _buttons;
    private readonly Vector4[] _buttonRects = new Vector4[MaxButtons];

    public static bool Replaces(Scene scene)
    {
        return scene.name == SceneName && TestGroups.IsEnabled(Feature.MenuCardBackground);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.MenuCardBackground)) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (Replaces(scene)) ApplyTo(scene);
        };
    }

    private static void ApplyTo(Scene scene)
    {
        var shader = Resources.Load<Shader>(ShaderResource);
        var atlas = Resources.Load<Texture2D>(AtlasResource);
        if (shader == null || !shader.isSupported || atlas == null)
        {
            Debug.LogWarning($"[MenuCardBackground] {ShaderResource} isn't available here; keeping the image background.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // Unity's missing components aren't C# null, so no ?. here.
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;
            Transform panel = canvas.transform.Find("Panel");
            if (panel == null) continue;
            var wall = panel.GetComponent<Image>();
            if (wall == null) continue;

            var material = new Material(shader) { name = "MenuCards (runtime)" };
            material.SetTexture(CardTexId, atlas);
            wall.sprite = null;
            wall.material = material;

            // Only the cards and the buttons: the glow, Brainy's jar, the
            // players and the title are hidden.
            foreach (Transform child in canvas.transform)
                if (child != panel && child.name != "ButtonContainer") child.gameObject.SetActive(false);

            AddTitle(canvas, material);

            var lighting = panel.gameObject.AddComponent<MenuCardBackground>();
            lighting._material = material;
            Transform container = canvas.transform.Find("ButtonContainer");
            lighting._buttons = container == null ? new RectTransform[0] :
                System.Array.ConvertAll(container.GetComponentsInChildren<Button>(true), b => (RectTransform)b.transform);
        }
    }

    // Each button's centre and half-size, in screen heights from the
    // screen's centre (the shader's units). The canvas is screen-space
    // overlay, so world space is screen pixels.
    private void LateUpdate()
    {
        float width = Screen.width, height = Screen.height;
        int count = 0;
        foreach (RectTransform button in _buttons)
        {
            if (button == null || !button.gameObject.activeInHierarchy || count == MaxButtons) continue;
            button.GetWorldCorners(Corners);
            Vector2 min = Corners[0], max = Corners[2];
            Vector2 centre = 0.5f * (min + max), half = 0.5f * (max - min);
            _buttonRects[count++] = new Vector4((centre.x - 0.5f * width) / height, (centre.y - 0.5f * height) / height,
                                                half.x * VisibleWidth / height, half.y * VisibleHeight / height);
        }
        _material.SetVectorArray(ButtonsId, _buttonRects);
        _material.SetFloat(ButtonCountId, count);
    }

    // The new title art, drawn last so it's above everything, the vignette
    // (on the wall) included. The wall's shader draws its shadow on the
    // cards, so it's told where the title is.
    private static void AddTitle(Canvas canvas, Material wall)
    {
        var sprite = Resources.Load<Sprite>(TitleResource);
        if (sprite == null) return;

        var go = new GameObject("Title", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, TitleY);
        rt.sizeDelta = new Vector2(TitleHeight * sprite.rect.width / sprite.rect.height, TitleHeight);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;

        // In screen heights; the canvas scales to match the screen's height.
        float referenceHeight = canvas.GetComponent<CanvasScaler>().referenceResolution.y;
        wall.SetTexture(TitleTexId, sprite.texture);
        wall.SetVector(TitleRectId, new Vector4(0f, TitleY, rt.sizeDelta.x, rt.sizeDelta.y) / referenceHeight);
    }
}
