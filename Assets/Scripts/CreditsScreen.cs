using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.CreditsMenu (groups 2 and 3): on the title screen, Review becomes
// Credits (MenuButtonPlates writes the label) at the bottom of the buttons,
// and Rules is greyed out for now. Credits opens a screen over the menu: the
// calm shader background (ShaderBackground), held still while the credits
// scroll over it with the wheel (or a drag), centred like a film's, each
// role small above its names in large type; a Menu button goes back. The
// click log times it as its own screen, "credits".
public static class CreditsScreen
{
    // Each role, and its names one per line. Edit here.
    private static readonly (string role, string names)[] Credits =
    {
        ("Lead Developer", "Charles Kahlenberg"),
        ("Developer, Artist", "Charles Campbell"),
        ("Computer Science & Education Mentor", "Dr. Lorraine Jaques"),
        ("Psychology Mentor", "Dr. Stephen Blessing"),
        ("Psychology Researchers", "Katlyn Nielsen\nChristina Black"),
        ("Built on the work of", "Matt [last name]\n[contributor]"),
    };

    // Canvas units (the title screen's canvas is 800 x 450).
    private const float TitleSize = 40f, NameSize = 28f, RoleSize = 14f;
    private const float RoleGap = 4f, EntryGap = 34f, Top = 70f, Margin = 16f;
    private const float ScrollSensitivity = 18f; // as TextPanel's
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
        ShaderBackground.AddCalm(screen);

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
            Line(content, role, TextBoxTheme.Regular, RoleSize, RoleInk, RoleGap);
            Line(content, names, MenuButtonPlates.Font, NameSize, NameInk, EntryGap);
        }

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

    // A centred line (or lines) of the credits, `gap` above the next.
    private static void Line(RectTransform content, string text, TMP_FontAsset font, float size, Color ink, float gap)
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
