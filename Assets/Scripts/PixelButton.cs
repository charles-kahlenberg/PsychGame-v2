using System.Collections.Generic;
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
//  - Refresh: bottom of the screen, with the refreshes left as diamonds in a
//    tab on top (GameManager.UpdateRefreshUI calls ShowRefreshesLeft). Under
//    the avatar; with Feature.Dictionary, in the middle, under the hand's
//    middle card (GameManager.RaiseHand lifts the hand clear of it).
//  - Back to Menu: bottom left, pinned to the corner and shrunk on narrow
//    screens (FitBesideHand).
// The save slots are blank plates that keep their own label (the save's name).
// Continue, Next, Submit and Menu are redone as the title screen's plate in
// green and blue, labelled in its font (Replate).
// The title screen's buttons are hand-drawn to match its art, so they stay.
public class PixelButton : MonoBehaviour
{
    private const string Folder = "PixelButtons/";
    private const float TexelsPerUnit = 4f;  // the art is saved at 4x so its pixels stay even once scaled
    private const float ShadowDepth = 2f;    // the drop shadow below each plate, in canvas units
    private const float ScreenMargin = 14f;
    private const float SubmitGap = 6f;
    private const float RefreshBottom = 3f; // tucked low; GameManager.HandBottomMargin keeps the cards clear of it

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

        bool dictionary = TestGroups.IsEnabled(Feature.Dictionary);
        var refresh = Restyle(c, "RefreshCardsButton", "refresh2");
        var middleCard = c.Find("Card3") as RectTransform;
        if (refresh != null && dictionary && middleCard != null)
        {
            // The cards are anchored at the canvas's center, so this holds at any screen shape.
            Place(c, refresh, new Vector2(LocalRect(c, middleCard).center.x, c.rect.yMin + RefreshBottom), new Vector2(0.5f, 0f));
        }
        else if (refresh != null)
        {
            var avatar = c.Find("Avatar") as RectTransform;
            float x = avatar != null ? LocalRect(c, avatar).center.x : c.rect.xMax - 90f;
            Place(c, refresh, new Vector2(x, c.rect.yMin + ScreenMargin), new Vector2(0.5f, 0f));
        }

        var back = Restyle(c, "BackButton", "menu");
        if (back != null) FitBesideHand.Pin(back, back.sizeDelta, right: false);

        // The save prompt's buttons (TextBoxTheme lays out the rest of it).
        Restyle(c, "SavePromptPanel/Yes", "save");
        Restyle(c, "SavePromptPanel/No", "skip");
    }

    // Next is the screen's one button, so its art is bigger. It's pinned to
    // the bottom-right corner, so it holds its place at any screen shape;
    // with ThemedTextBoxes, under Brainy in the right-hand column.
    private static void ApplyToGrading(RectTransform c)
    {
        var next = Restyle(c, "NextButton", "next");
        if (next == null) return;

        float x = TextBoxTheme.Enabled ? TextBoxTheme.GradingColumnX : -ScreenMargin - next.sizeDelta.x / 2f;
        next.anchorMin = next.anchorMax = new Vector2(1f, 0f);
        next.pivot = new Vector2(0.5f, 0f);
        next.anchoredPosition = new Vector2(x, TextBoxTheme.ScreenMargin - ShadowDepth);
    }

    // `name` is a path under the canvas. The plate is centered where the button was.
    private static RectTransform Restyle(RectTransform canvas, string name, string sprites, bool keepLabel = false)
    {
        var t = canvas.Find(name) as RectTransform;
        var button = t != null ? t.GetComponent<Button>() : null;
        var image = button != null ? button.GetComponent<Image>() : null;
        if (image == null) return null;
        if (Plates.TryGetValue(sprites, out var redone)) return Replate(canvas, t, button, image, redone);
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

    // The redone buttons: a blank plate (Resources/ButtonPlates, the title
    // screen's plate in other colours) with the label written on in the title
    // screen's font, moving as the title screen's do (MenuButtonPlates).
    // Labels are in the title screen's ink, shrinking to fit their plate.
    // Refresh is its own plate (Resources/ButtonPlates/Refresh),
    // drawn at its art's shape, with a tab on top whose two diamonds light up
    // for the refreshes left (ShowRefreshesLeft).
    private static readonly Dictionary<string, (string plate, string label, Vector2 size)> Plates =
        new Dictionary<string, (string, string, Vector2)>
        {
            { "continue", ("ButtonPlates/Green", "Continue", new Vector2(156f, 36f)) },
            { "next", ("ButtonPlates/Green", "Next", new Vector2(116f, 36f)) },
            { "submit", ("ButtonPlates/Green", "Submit", new Vector2(100f, 32f)) },
            { "menu", ("ButtonPlates/Blue", "Menu", new Vector2(100f, 32f)) },
            { "refresh2", ("ButtonPlates/Refresh", "Refresh", new Vector2(108f, 42f)) },
        };
    private const float LabelSize = 10f; // a little under the title screen's, for room round it
    private static readonly Color GreenInk = new Color32(0x18, 0x29, 0x37, 0xFF); // the title screen's ink, darker on the green plate

    // The Refresh plate's 628 x 244 art: its face below the tab (for the
    // label), and where each lit diamond (Resources/ButtonPlates/
    // RefreshDiamondLeft/Right) sits in the tab's slots, in texels from the top left.
    private const float RefreshArtW = 628f, RefreshArtH = 244f;
    private static readonly Rect RefreshFace = Rect.MinMaxRect(20f, 52f, 607f, 195f);
    private static readonly Rect[] RefreshDiamonds = { new Rect(266f, 14f, 34f, 34f), new Rect(328f, 14f, 34f, 34f) };

    private GameObject[] _diamonds; // the Refresh plate's lit diamonds, left then right
    private bool _diamondsShown;

    private static RectTransform Replate(RectTransform canvas, RectTransform t, Button button, Image image,
        (string plate, string label, Vector2 size) redone)
    {
        var sprite = Resources.Load<Sprite>(redone.plate);
        if (sprite == null || MenuButtonPlates.Font == null)
        {
            Debug.LogWarning($"[PixelButton] Resources/{redone.plate} or the title font is missing; leaving {t.name} as it is.");
            return null;
        }

        foreach (var text in t.GetComponentsInChildren<TMP_Text>(true)) text.enabled = false;
        Vector2 center = LocalRect(canvas, t).center;
        MenuButtonPlates.Restyle(button, image, sprite, redone.label, redone.size,
            redone.plate == "ButtonPlates/Green" ? GreenInk : MenuButtonPlates.Ink);
        if (t.Find("Plate/PlateLabel") is Transform label && label.TryGetComponent(out TMP_Text written))
        {
            written.enableAutoSizing = true;
            written.fontSizeMin = 8f;
            written.fontSizeMax = LabelSize;
            if (redone.plate == "ButtonPlates/Refresh") AddRefreshTab(t, written);
        }
        Place(canvas, t, center, new Vector2(0.5f, 0.5f));
        return t;
    }

    // On the Refresh plate: the label on the face below the tab (shrinking if
    // it must), and the lit diamonds over the tab's slots, on the plate so
    // they lift with it.
    private static void AddRefreshTab(RectTransform t, TMP_Text label)
    {
        var plate = (RectTransform)t.Find("Plate");
        Anchor(label.rectTransform, RefreshFace);
        label.rectTransform.offsetMin = new Vector2(10f, 0f);
        label.rectTransform.offsetMax = new Vector2(-10f, 0f);

        var pixel = t.gameObject.AddComponent<PixelButton>();
        pixel._button = t.GetComponent<Button>();
        pixel._diamonds = new GameObject[RefreshDiamonds.Length];
        string[] names = { "RefreshDiamondLeft", "RefreshDiamondRight" };
        for (int i = 0; i < names.Length; i++)
        {
            var go = new GameObject(names[i], typeof(RectTransform), typeof(Image));
            go.transform.SetParent(plate, false);
            Anchor((RectTransform)go.transform, RefreshDiamonds[i]);
            var image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("ButtonPlates/" + names[i]);
            image.raycastTarget = false;
            pixel._diamonds[i] = go;
        }
    }

    // Stretches a child over a rectangle of the Refresh art (texels from its top left).
    private static void Anchor(RectTransform rt, Rect texels)
    {
        rt.anchorMin = new Vector2(texels.xMin / RefreshArtW, 1f - texels.yMax / RefreshArtH);
        rt.anchorMax = new Vector2(texels.xMax / RefreshArtW, 1f - texels.yMin / RefreshArtH);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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

    // A diamond lit for each refresh left, from the left; with none left the
    // plate greys out as Refresh can't be pressed (GameManager disables it).
    public void ShowRefreshesLeft(int left)
    {
        if (_diamonds != null)
        {
            for (int i = 0; i < _diamonds.Length; i++)
            {
                GameObject d = _diamonds[i];
                LeanTween.cancel(d);
                bool lit = i < left;
                // A spent diamond swells and pops away; the first call (the hand
                // being shown, maybe from a save) just sets them.
                if (!lit && d.activeSelf && _diamondsShown)
                {
                    LeanTween.scale(d, Vector3.one * 1.6f, 0.1f).setEaseOutQuad().setOnComplete(() =>
                        LeanTween.scale(d, Vector3.zero, 0.15f).setEaseInBack().setOnComplete(() => d.SetActive(false)));
                    continue;
                }
                d.transform.localScale = Vector3.one;
                d.SetActive(lit);
            }
            _diamondsShown = true;
            return;
        }
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
