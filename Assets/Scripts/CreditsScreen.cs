using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.CreditsMenu (groups 2 and 3): on the title screen, Review becomes
// Credits (MenuButtonPlates writes the label) at the bottom of the buttons,
// and Rules is greyed out for now. Credits opens a screen over the menu: the
// calm shader background (ShaderBackground), and the credits scrolled with
// the wheel (or a drag), taking the background's pattern along with them,
// centred like a film's, each role small above its names in large type
// (Atkinson Hyperlegible, under a Righteous title); a Menu button goes back. The
// click log times it as its own screen, "credits".
//
// The easter eggs come last. Clicking the horse's name shows a horse
// (Resources/Horse) over the middle of the screen with a whinny
// (Resources/Sounds/Horse), fading out over a second.
public static class CreditsScreen
{
    // Each role (blank: none), and its names one per line. Edit here.
    private static readonly (string role, string names)[] Credits =
    {
        ("Lead Developer", "Charles Kahlenberg"),
        ("Developer, Artist", "Charles Campbell"),
        ("Computer Science & Education Mentor", "Dr. Lorraine Jaques"),
        ("Psychology Mentor", "Dr. Stephen Blessing"),
        ("Psychology Researchers", "Katlyn Nielsen\nChristina Black"),
        ("Built on the work of", "Matt [last name]\n[contributor]"),
        ("Lead Detective", "Hercule Poirot"),
        ("horse", Horse),
    };
    private const string Horse = "horse"; // the name that scares
    private const string Last = "The trophy is a lie"; // alone at the very end, LastGap below the rest

    // Canvas units (the title screen's canvas is 800 x 450).
    private const float TitleSize = 60f, NameSize = 36f, RoleSize = 24f;
    private const float RoleGap = 4f, EntryGap = 42f, Top = 70f, Margin = 16f;
    private const float ScrollSensitivity = 18f; // as TextPanel's
    private const float HorseHeight = 0.9f; // of the screen's height
    private const float LastGap = 320f;     // most of a screen, so the credits seem over
    private static readonly Color NameInk = TextBoxTheme.Paper;
    private static readonly Color RoleInk = new Color32(0x8C, 0x9E, 0xFF, 0xFF); // the shader's excited contour blue

    public static bool Enabled => TestGroups.IsEnabled(Feature.CreditsMenu);

    // From SplashUI.Start, after the menu's buttons are plated.
    public static void SetUpMenu(Scene scene)
    {
        if (!Enabled) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform container = root.transform.Find("ButtonContainer");
            if (container == null) continue;

            if (container.Find("Rules")?.GetComponent<Button>() is Button rules) rules.interactable = false;

            if (container.Find("Review")?.GetComponent<Button>() is Button credits)
            {
                credits.name = "Credits"; // as the click log names it
                credits.transform.SetAsLastSibling();
                credits.onClick = new Button.ButtonClickedEvent(); // drops the scene's Review listener
                var canvas = (RectTransform)root.transform;
                credits.onClick.AddListener(() => Open(canvas));
            }
        }
    }

    private static void Open(RectTransform canvas)
    {
        var screen = NewChild("CreditsScreen", canvas);
        Stretch(screen);
        // Blocks the menu beneath; seen only where the shader can't run.
        screen.gameObject.AddComponent<Image>().color = new Color32(0x13, 0x19, 0x26, 0xFF);
        ShaderBackground background = ShaderBackground.AddCalm(screen);

        // The whole screen takes the wheel; only the credits move.
        var view = NewChild("Scroll", screen);
        Stretch(view);
        view.gameObject.AddComponent<Image>().color = Color.clear; // catches the wheel between the lines
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = ScrollSensitivity;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = view;

        var content = NewChild("Credits", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        scroll.content = content;
        if (background != null)
            scroll.onValueChanged.AddListener(_ => background.Offset = content.anchoredPosition.y / view.rect.height);
        var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
        // Room at the bottom for the last names to scroll up to the middle.
        column.padding = new RectOffset(0, 0, (int)Top, (int)(canvas.rect.height / 2f));
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Line(content, "Credits", MenuButtonPlates.Font, TitleSize, NameInk, EntryGap);
        foreach (var (role, names) in Credits)
        {
            if (role != "") Line(content, role, TextBoxTheme.Regular, RoleSize, RoleInk, RoleGap);
            TMP_Text name = Line(content, names, TextBoxTheme.SemiBold, NameSize, NameInk, EntryGap);
            if (names != Horse) continue;
            // A link, so only the word itself is clickable; underlined while hovered.
            name.name = "Horse"; // as the click log names it
            name.text = $"<link=\"{Horse}\">{Horse}</link>";
            name.raycastTarget = true;
            var horse = name.gameObject.AddComponent<TextLinkClicks>();
            horse.Clicked = _ => Jumpscare(screen);
            horse.Hovered = link => name.fontStyle = link != null ? FontStyles.Underline : FontStyles.Normal;
        }
        NewChild("Gap", content).gameObject.AddComponent<LayoutElement>().minHeight = LastGap;
        Line(content, Last, TextBoxTheme.SemiBold, NameSize, NameInk, 0f);

        var back = NewChild("MenuButton", screen); // after the scroll, so it stays on top
        back.anchorMin = back.anchorMax = back.pivot = Vector2.zero;
        back.anchoredPosition = new Vector2(Margin, Margin);
        var button = back.gameObject.AddComponent<Button>();
        var plate = Resources.Load<Sprite>("ButtonPlates/Blue");
        if (plate != null && MenuButtonPlates.Font != null)
        {
            // As the other screens' Menu buttons (PixelButton).
            MenuButtonPlates.Restyle(button, back.gameObject.AddComponent<Image>(), plate, "Menu", new Vector2(100f, 32f), MenuButtonPlates.Ink);
            back.GetComponentInChildren<TMP_Text>().fontSize = 10f;
        }
        button.onClick.AddListener(() =>
        {
            Object.Destroy(screen.gameObject);
            ClickLogger.SwitchScreen("SplashScene");
        });

        ClickLogger.SwitchScreen("CreditsScene");
    }

    // The horse pops up in the middle with a whinny and fades over a second.
    // Loaded as a texture, so Horse.jpg works with its default import settings.
    private static void Jumpscare(RectTransform screen)
    {
        var picture = Resources.Load<Texture2D>("Horse");
        Sfx.Horse();
        if (picture == null) return;

        var horse = NewChild("Horse", screen);
        float height = screen.rect.height * HorseHeight;
        horse.sizeDelta = new Vector2(height * picture.width / picture.height, height);
        var image = horse.gameObject.AddComponent<Image>();
        image.sprite = Sprite.Create(picture, new Rect(0f, 0f, picture.width, picture.height), new Vector2(0.5f, 0.5f));
        image.raycastTarget = false;
        var group = horse.gameObject.AddComponent<CanvasGroup>();
        LeanTween.alphaCanvas(group, 0f, 1f).setEaseInQuad() // held near full at first, then gone
            .setOnComplete(() =>
            {
                Object.Destroy(image.sprite);
                Object.Destroy(horse.gameObject);
            });
    }

    // A centred line (or lines) of the credits, `gap` above the next.
    private static TMP_Text Line(RectTransform content, string text, TMP_FontAsset font, float size, Color ink, float gap)
    {
        var t = NewChild("Line", content).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = ink;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.margin = new Vector4(Margin, 0f, Margin, 0f);
        NewChild("Gap", content).gameObject.AddComponent<LayoutElement>().minHeight = gap;
        return t;
    }

    private static RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
