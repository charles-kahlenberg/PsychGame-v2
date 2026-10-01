using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.PixelButtons (Group 2): the game's buttons become pixel-art plates
// drawn like the card backs (Resources/PixelButtons, made by
// Tools/PixelButtons/make_pixel_buttons.py). The labels are part of the art,
// so each button's own text is hidden; hover, press and disabled are sprite
// swaps. Built at runtime on each scene's existing buttons, so their click
// handlers and logging are unchanged. Each plate sits where its button was,
// except on the response screen (GameScene):
//  - Submit: just right of the response box, level with its bottom edge.
//  - Refresh: bottom of the screen, under the avatar, with the refreshes left
//    as diamonds in a tab on top (GameManager.UpdateRefreshUI calls
//    ShowRefreshesLeft).
//  - Back to Menu: bottom left, drawn as a back arrow and "MENU".
// The save slots are blank plates that keep their own label (the save's name).
// The title screen's buttons are hand-drawn to match its art, so they stay.
public class PixelButton : MonoBehaviour
{
    private const string Folder = "PixelButtons/";
    private const float TexelsPerUnit = 4f;  // the art is saved at 4x so its pixels stay even once scaled
    private const float ShadowDepth = 2f;    // the drop shadow below each plate, in canvas units
    private const float ScreenMargin = 14f;
    private const float SubmitGap = 6f;

    private static readonly Color LabelInk = new Color32(0xF7, 0xF1, 0xE6, 0xFF);    // the art's cream
    private static readonly Color LabelDimInk = new Color32(0xB9, 0xB9, 0xC2, 0xFF); // ...and its disabled grey

    public static bool Enabled => TestGroups.IsEnabled(Feature.PixelButtons);

    private Button _button;
    private Image _image;
    private TMP_Text _label; // only on plates that keep their own text

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled) return;
        SceneManager.sceneLoaded += (scene, mode) => Apply(scene);
    }

    private static void Apply(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;
            var c = (RectTransform)canvas.transform;

            switch (scene.name)
            {
                case "GameScene": ApplyToGame(c); break;
                case "IntroductionScene": Restyle(c, "Continue", "continue"); break;
                case "RulesScene":
                    Restyle(c, "ContinueButton", "continue");
                    Restyle(c, "ShowExampleButton", "example");
                    Restyle(c, "ReturnButton", "menu");
                    break;
                case "GradingScene": ApplyToGrading(c); break;
                case "ReviewScene": Restyle(c, "Button", "menu"); break;
                case "GameEndScene": Restyle(c, "BackButton", "menu"); break;
                case "SaveSelectScene":
                    for (int i = 1; i <= 3; i++) Restyle(c, $"SaveSlot{i}Button", "slot", keepLabel: true);
                    Restyle(c, "BackButton", "menu");
                    break;
            }
        }
    }

    private static void ApplyToGame(RectTransform c)
    {
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

        // The save prompt's buttons (TextBoxTheme lays out the rest of it).
        Restyle(c, "SavePromptPanel/Yes", "save");
        Restyle(c, "SavePromptPanel/No", "skip");
    }

    // Next is the screen's one button, so its art is bigger. Like Submit, it
    // sits just right of its box (Brainy's feedback panel, which TextBoxTheme
    // insets by its screen margin), level with the box's bottom edge.
    private static void ApplyToGrading(RectTransform c)
    {
        var next = Restyle(c, "NextButton", "next");
        var feedback = c.Find("AIResponseScrollView") as RectTransform;
        if (next == null || feedback == null) return;

        Rect box = LocalRect(c, feedback);
        float inset = TextBoxTheme.Enabled ? TextBoxTheme.ScreenMargin : 0f;
        Place(c, next, new Vector2(box.xMax - inset + SubmitGap, box.yMin + inset - ShadowDepth), new Vector2(0f, 0f));
    }

    // `name` is a path under the canvas. The plate is centered where the button was.
    private static RectTransform Restyle(RectTransform canvas, string name, string sprites, bool keepLabel = false)
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

        TMP_Text label = null;
        foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
        {
            if (keepLabel && label == null) label = text;
            else text.enabled = false;
        }
        Vector2 center = LocalRect(canvas, t).center;

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
        t.localScale = Vector3.one;
        var pixel = t.gameObject.AddComponent<PixelButton>();
        pixel._button = button;
        pixel._image = image;
        pixel.Show(sprites);
        Place(canvas, t, center, new Vector2(0.5f, 0.5f));

        if (label != null)
        {
            pixel._label = label;
            StyleLabel(label);
        }
        return t;
    }

    // A kept label, set on the plate in the art's cream. The plate sits half
    // a unit above the image's middle (the drop shadow is below it).
    private static void StyleLabel(TMP_Text label)
    {
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 1f);
        rt.offsetMax = new Vector2(-10f, 0f);
        if (TextBoxTheme.SemiBold != null)
        {
            label.font = TextBoxTheme.SemiBold;
            label.fontSharedMaterial = TextBoxTheme.SemiBold.material;
        }
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 15f;
        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.margin = Vector4.zero;
    }

    // Kept labels dim with the plate when it can't be pressed (an empty save slot).
    private void Update()
    {
        if (_label != null) _label.color = _button.IsInteractable() ? LabelInk : LabelDimInk;
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
        var parent = (RectTransform)button.parent;
        button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
        button.pivot = pivot;
        button.anchoredPosition = (Vector2)parent.InverseTransformPoint(canvas.TransformPoint(point)) - parent.rect.center;
    }

    // A child's rectangle in the canvas's own space.
    public static Rect LocalRect(RectTransform canvas, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector2 min = canvas.InverseTransformPoint(corners[0]);
        Vector2 max = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
