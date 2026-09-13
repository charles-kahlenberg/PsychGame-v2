using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds a simple full-screen "enter a username" overlay purely at runtime,
// the same way ClickLogger bootstraps itself with no scene setup required.
// Blocks interaction with whatever's underneath until submitted or skipped.
internal static class UsernamePromptUI
{
    public static void Show(string prefillUsername, System.Action<string> onSubmit)
    {
        EnsureEventSystem();

        var canvasGO = new GameObject("UsernamePromptCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        Object.DontDestroyOnLoad(canvasGO);

        var background = CreateUIObject("Background", canvasGO.transform);
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.75f);
        StretchToFill(background.GetComponent<RectTransform>());

        var panel = CreateUIObject("Panel", background.transform);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.12f, 0.12f, 0.12f, 0.97f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460, 240);
        panelRect.anchoredPosition = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var label = CreateUIObject("Label", panel.transform);
        var labelText = label.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 22;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.text = "Enter a username\n(optional, helps identify your sessions)";
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0, -20);
        labelRect.sizeDelta = new Vector2(-40, 70);

        var inputGO = CreateUIObject("UsernameInputField", panel.transform);
        var inputImage = inputGO.AddComponent<Image>();
        inputImage.color = Color.white;
        var inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.anchorMin = inputRect.anchorMax = new Vector2(0.5f, 1f);
        inputRect.pivot = new Vector2(0.5f, 1f);
        inputRect.sizeDelta = new Vector2(360, 44);
        inputRect.anchoredPosition = new Vector2(0, -100);

        var textArea = CreateUIObject("Text", inputGO.transform);
        var inputText = textArea.AddComponent<Text>();
        inputText.font = font;
        inputText.fontSize = 20;
        inputText.color = Color.black;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(12, 6);
        textAreaRect.offsetMax = new Vector2(-12, -6);

        var placeholder = CreateUIObject("Placeholder", inputGO.transform);
        var placeholderText = placeholder.AddComponent<Text>();
        placeholderText.font = font;
        placeholderText.fontSize = 20;
        placeholderText.color = new Color(0f, 0f, 0f, 0.5f);
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.text = "Username";
        var placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(12, 6);
        placeholderRect.offsetMax = new Vector2(-12, -6);

        var inputField = inputGO.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.characterLimit = 40;
        if (!string.IsNullOrEmpty(prefillUsername))
        {
            inputField.text = prefillUsername;
        }

        var buttonGO = CreateUIObject("ContinueButton", panel.transform);
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.55f, 0.9f, 1f);
        var buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.sizeDelta = new Vector2(180, 48);
        buttonRect.anchoredPosition = new Vector2(0, 26);
        var button = buttonGO.AddComponent<Button>();

        var buttonLabel = CreateUIObject("Text", buttonGO.transform);
        var buttonLabelText = buttonLabel.AddComponent<Text>();
        buttonLabelText.font = font;
        buttonLabelText.fontSize = 20;
        buttonLabelText.alignment = TextAnchor.MiddleCenter;
        buttonLabelText.color = Color.white;
        buttonLabelText.text = "Continue";
        StretchToFill(buttonLabel.GetComponent<RectTransform>());

        bool submitted = false;
        void Submit()
        {
            if (submitted) return; // guard against both the button click and Enter key firing
            submitted = true;

            string username = inputField.text.Trim();
            Object.Destroy(canvasGO);
            onSubmit(username);
        }

        button.onClick.AddListener(Submit);
        inputField.onSubmit.AddListener(_ => Submit());

        EventSystem.current.SetSelectedGameObject(inputGO);
        inputField.ActivateInputField();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchToFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(esGO);
    }
}
