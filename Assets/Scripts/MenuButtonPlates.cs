using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.MenuButtonPlates (Group 2): the title screen's (SplashScene)
// buttons, whose labels were drawn into each image, become the blank stone
// plate (Resources/MenuButton) with the label written on it in Righteous
// (Resources/Fonts/Righteous, a wide pixel font, made into a TMP font at
// runtime). The plates are bigger than the old art, and the font is wide, so
// the plate is 9-sliced (Resources/MenuButton.png.meta): its ends keep their
// shape and only the middle stretches to the width. The gaps between them
// are trimmed so the stack still clears the title.
//
// They move like the PixelButtons: the plate (a child, with the label on it)
// lifts on hover and is pressed down when clicked, darkening, while the
// button itself, and so its shadow on the cards, stays put. This replaces
// the buttons' animator, which grew them on hover.
public class MenuButtonPlates : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const string SceneName = "SplashScene";
    private const string PlateResource = "MenuButton";
    private const string FontResource = "Fonts/Righteous";

    // Canvas units; the old buttons were 119 x 31 with 25 between.
    private const float Width = 226f, Height = 44f;
    private const float Spacing = 14f;

    // One size for every label, so they match; "Reset Saves", the longest,
    // is ~14.7 of it wide, which fits the face with room to spare.
    private const float FontSize = 12f;

    // The plate's pale face within the image (the rest is its rim and the
    // dark stone lip below): its top and bottom as anchors, and an inset
    // from each end, so the label centres on the face.
    private const float FaceBottom = 0.25f, FaceTop = 0.94f, FaceInset = 10f;
    private static readonly Color Ink = new Color32(0x3A, 0x30, 0x30, 0xFF);
    private static readonly Color DimInk = new Color32(0x3A, 0x30, 0x30, 0x80); // Load Game with no save

    // As the PixelButtons: up a unit on hover, down two (onto the shadow)
    // when pressed, a step darker when pressed and grey when disabled.
    private const float Lift = 1f, Press = 2f, MoveTime = 0.06f;
    private static readonly Color PressedTint = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color DisabledTint = new Color(0.72f, 0.72f, 0.75f);

    // The visible plate within the 628 x 202 image (600 x 196 of it; the
    // ends' 14 clear texels keep their size when the middle stretches), for
    // the drop shadows MenuCardBackground draws.
    private const float ImageHeight = 202f;
    public const float VisibleWidth = 1f - 2f * 14f * (Height / ImageHeight) / Width;
    public const float VisibleHeight = 196f / ImageHeight;

    private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
    {
        { "NewGameButton", "New Game" },
        { "ContinueButton", "Load Game" },
        { "Rules", "Rules" },
        { "Review", "Review" },
        { "ResetGame", "Reset Saves" },
    };

    private static TMP_FontAsset _font;

    public static bool Enabled => TestGroups.IsEnabled(Feature.MenuButtonPlates);

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
        var plate = Resources.Load<Sprite>(PlateResource);
        if (_font == null)
        {
            var source = Resources.Load<Font>(FontResource);
            if (source != null) _font = TMP_FontAsset.CreateFontAsset(source);
        }
        if (plate == null || _font == null)
        {
            Debug.LogWarning($"[MenuButtonPlates] {PlateResource} or {FontResource} is missing; keeping the drawn buttons.");
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) continue;
            Transform container = canvas.transform.Find("ButtonContainer");
            if (container == null) continue;

            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.spacing = Spacing;

            foreach (Button button in container.GetComponentsInChildren<Button>(true))
            {
                if (!Labels.TryGetValue(button.name, out string label)) continue;
                var hitArea = button.GetComponent<Image>();
                if (hitArea == null) continue;
                Restyle(button, hitArea, plate, label);
            }
        }
    }

    private Button _button;
    private RectTransform _plate;
    private TMP_Text _label;
    private bool _hovered, _pressed;

    // The button's own image stays as the invisible click target; the plate
    // is drawn by a child so it can move without moving the button.
    private static void Restyle(Button button, Image hitArea, Sprite sprite, string label)
    {
        var animator = button.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        var pop = button.GetComponent<ButtonHoverPop>();
        if (pop != null) Destroy(pop);
        button.transform.localScale = Vector3.one;
        ((RectTransform)button.transform).sizeDelta = new Vector2(Width, Height);

        hitArea.sprite = null;
        hitArea.color = Color.clear;

        var go = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(button.transform, false);
        var plate = (RectTransform)go.transform;
        plate.anchorMin = Vector2.zero;
        plate.anchorMax = Vector2.one;
        plate.offsetMin = plate.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        // Draw the ends at the same scale the height is drawn at.
        image.pixelsPerUnitMultiplier = sprite.rect.height / (Height * image.pixelsPerUnit);
        image.raycastTarget = false;

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white, highlightedColor = Color.white, selectedColor = Color.white,
            pressedColor = PressedTint, disabledColor = DisabledTint,
            colorMultiplier = 1f, fadeDuration = MoveTime,
        };

        var plates = button.gameObject.AddComponent<MenuButtonPlates>();
        plates._button = button;
        plates._plate = plate;
        plates._label = AddLabel(plate, label);
    }

    public void OnPointerEnter(PointerEventData e) { _hovered = true; Move(); }
    public void OnPointerExit(PointerEventData e) { _hovered = false; Move(); }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { _pressed = true; Move(); } }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { _pressed = false; Move(); } }

    private void Move()
    {
        float y = !_button.IsInteractable() ? 0f : _pressed ? -Press : _hovered ? Lift : 0f;
        LeanTween.cancel(_plate.gameObject);
        LeanTween.value(_plate.gameObject, _plate.anchoredPosition.y, y, MoveTime)
            .setOnUpdate(v => _plate.anchoredPosition = new Vector2(0f, v));
    }

    private void Update()
    {
        _label.color = _button.IsInteractable() ? Ink : DimInk;
    }

    // Hidden mid-hover (the scene changing), it comes back at rest.
    private void OnDisable()
    {
        _hovered = _pressed = false;
        if (_plate == null) return;
        LeanTween.cancel(_plate.gameObject);
        _plate.anchoredPosition = Vector2.zero;
    }

    private static TMP_Text AddLabel(Transform plate, string label)
    {
        var go = new GameObject("PlateLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(plate, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, FaceBottom);
        rt.anchorMax = new Vector2(1f, FaceTop);
        rt.offsetMin = new Vector2(FaceInset, 0f);
        rt.offsetMax = new Vector2(-FaceInset, 0f);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = label;
        text.color = Ink;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontSize = FontSize;
        text.raycastTarget = false;
        return text;
    }
}
