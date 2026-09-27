using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.PixelButtons (Group 2): the response screen's Submit, Refresh and
// Back to Menu buttons become pixel-art plates drawn like the card backs
// (Resources/PixelButtons, made by Tools/PixelButtons/make_pixel_buttons.py).
// The labels are part of the art, so each button's own text is hidden; hover,
// press and disabled are sprite swaps. Refresh shows the refreshes left as
// diamonds in a tab on top (GameManager.UpdateRefreshUI calls
// ShowRefreshesLeft). Built at runtime on GameScene's existing buttons, so
// their click handlers and logging are unchanged.
//  - Submit: just right of the response box, level with its bottom edge.
//  - Refresh: bottom of the screen, under the avatar.
//  - Back to Menu: bottom left, drawn as a back arrow and "MENU".
public class PixelButton : MonoBehaviour
{
    private const string SceneName = "GameScene";
    private const string Folder = "PixelButtons/";
    private const float TexelsPerUnit = 4f;  // the art is saved at 4x so its pixels stay even once scaled
    private const float ShadowDepth = 2f;    // the drop shadow below each plate, in canvas units
    private const float ScreenMargin = 14f;
    private const float SubmitGap = 6f;

    public static bool Enabled => TestGroups.IsEnabled(Feature.PixelButtons);

    private Button _button;
    private Image _image;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == SceneName) Apply(scene);
        };
    }

    private static void Apply(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;
            var c = (RectTransform)canvas.transform;

            var submit = Restyle(c, "SubmitButton", "submit");
            var response = c.Find("ResponseInput") as RectTransform;
            if (submit != null && response != null)
            {
                Rect box = LocalRect(c, response);
                Place(c, submit, new Vector2(box.xMax + SubmitGap, box.yMin - ShadowDepth), new Vector2(0f, 0f));
            }

            var refresh = Restyle(c, "RefreshCardsButton", "refresh2");
            if (refresh != null)
            {
                var avatar = c.Find("Avatar") as RectTransform;
                float x = avatar != null ? LocalRect(c, avatar).center.x : c.rect.xMax - 90f;
                Place(c, refresh, new Vector2(x, c.rect.yMin + ScreenMargin), new Vector2(0.5f, 0f));
            }

            var back = Restyle(c, "BackButton", "menu");
            if (back != null)
                Place(c, back, new Vector2(c.rect.xMin + ScreenMargin, c.rect.yMin + ScreenMargin), new Vector2(0f, 0f));
        }
    }

    private static RectTransform Restyle(RectTransform canvas, string name, string sprites)
    {
        var t = canvas.Find(name) as RectTransform;
        var button = t != null ? t.GetComponent<Button>() : null;
        var image = button != null ? button.GetComponent<Image>() : null;
        if (image == null) return null;
        if (Load(sprites + "_normal") == null)
        {
            Debug.LogWarning($"[PixelButton] Resources/{Folder}{sprites}_normal is missing; leaving {name} as it is.");
            return null;
        }

        foreach (var text in t.GetComponentsInChildren<TMP_Text>(true)) text.enabled = false;

        // The art has its own hover lift, so it doesn't also grow on hover.
        var pop = t.GetComponent<ButtonHoverPop>();
        if (pop != null) Destroy(pop);

        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = false;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        image.canvasRenderer.SetColor(Color.white); // clears the tint the old colour transition left on it

        // Fixed to the art's size from here on (some were stretched by their anchors).
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);

        var pixel = t.gameObject.AddComponent<PixelButton>();
        pixel._button = button;
        pixel._image = image;
        pixel.Show(sprites);
        return t;
    }

    public void ShowRefreshesLeft(int left)
    {
        Show(left <= 0 ? "refresh0" : "refresh" + Mathf.Min(left, 2));
    }

    private void Show(string sprites)
    {
        Sprite normal = Load(sprites + "_normal");
        Sprite disabled = Load(sprites + "_disabled");
        if (normal == null) normal = disabled; // none left: Refresh only has a disabled look
        if (normal == null) return;

        _image.sprite = normal;
        _button.spriteState = new SpriteState
        {
            highlightedSprite = Load(sprites + "_hover"),
            pressedSprite = Load(sprites + "_pressed"),
            selectedSprite = null, // after a click, back to the normal look
            disabledSprite = disabled,
        };
        ((RectTransform)transform).sizeDelta = normal.rect.size / TexelsPerUnit;
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>(Folder + name);

    // Pins a button by its pivot to a point in the canvas's own space.
    private static void Place(RectTransform canvas, RectTransform button, Vector2 point, Vector2 pivot)
    {
        button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
        button.pivot = pivot;
        button.anchoredPosition = point - canvas.rect.center;
    }

    // A child's rectangle in the canvas's own space.
    private static Rect LocalRect(RectTransform canvas, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector2 min = canvas.InverseTransformPoint(corners[0]);
        Vector2 max = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
