using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.CreditsMenu (groups 2 and 3): on the title screen, Review becomes
// Credits at the bottom of the buttons, and Rules is greyed out for now.
// Credits opens a blank screen over the menu, the calm shader background
// (ShaderBackground) with a Menu button back; the credits themselves are
// still to come. The click log times it as its own screen, "credits".
public static class CreditsScreen
{
    private const float Margin = 16f;

    public static bool Enabled => TestGroups.IsEnabled(Feature.CreditsMenu);

    // From SplashUI.Start, after MenuButtonPlates has written the labels.
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
                var label = credits.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "Credits";
                credits.onClick = new Button.ButtonClickedEvent(); // drops the scene's Review listener
                var canvas = (RectTransform)root.transform;
                credits.onClick.AddListener(() => Open(canvas));
            }
        }
    }

    private static void Open(RectTransform canvas)
    {
        var screen = NewChild("CreditsScreen", canvas);
        screen.anchorMin = Vector2.zero;
        screen.anchorMax = Vector2.one;
        screen.offsetMin = screen.offsetMax = Vector2.zero;
        // Blocks the menu beneath; seen only where the shader can't run.
        screen.gameObject.AddComponent<Image>().color = new Color32(0x1C, 0x24, 0x3A, 0xFF);
        ShaderBackground.AddCalm(screen);

        var back = NewChild("MenuButton", screen);
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

    private static RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }
}
