using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

// A small popup, built at runtime like UsernamePromptUI, telling a player
// whose browser draws in software (no graphics card) how to fix it. index.html
// detects that and already drops the render resolution; this just explains.
internal static class GraphicsNotice
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsSoftwareRendering();

    public static bool SoftwareRendering => IsSoftwareRendering() != 0;
#else
    public static bool SoftwareRendering => false;
#endif

    public static void Show()
    {
        UsernamePromptUI.EnsureEventSystem();

        var canvasGO = new GameObject("GraphicsNoticeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32759; // just under the username prompt
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // No full-screen blocker: the rest of the main screen stays usable.
        var panel = UsernamePromptUI.CreateUIObject("Panel", canvasGO.transform);
        panel.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(820, 160);
        panelRect.anchoredPosition = new Vector2(0, -24);

        Font font = TextBoxTheme.Regular.sourceFontFile;

        var label = UsernamePromptUI.CreateUIObject("Label", panel.transform);
        var labelText = label.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 20;
        labelText.resizeTextForBestFit = true; // shrink rather than clip
        labelText.resizeTextMinSize = 12;
        labelText.resizeTextMaxSize = 20;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = "Your browser isn't using your graphics card, so the game may run slowly.\n" +
                         "Turn on \"Use graphics acceleration when available\" in your browser's settings, then restart the browser.";
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(20, 56);
        labelRect.offsetMax = new Vector2(-20, -12);

        var buttonGO = UsernamePromptUI.CreateUIObject("OkButton", panel.transform);
        buttonGO.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.9f, 1f);
        var buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(120, 38);
        buttonRect.anchoredPosition = new Vector2(0, 12);
        buttonGO.AddComponent<Button>().onClick.AddListener(() => Object.Destroy(canvasGO));

        var buttonLabel = UsernamePromptUI.CreateUIObject("Text", buttonGO.transform);
        var buttonLabelText = buttonLabel.AddComponent<Text>();
        buttonLabelText.font = font;
        buttonLabelText.fontSize = 18;
        buttonLabelText.alignment = TextAnchor.MiddleCenter;
        buttonLabelText.color = Color.white;
        buttonLabelText.text = "OK";
        UsernamePromptUI.StretchToFill(buttonLabel.GetComponent<RectTransform>());
    }
}
