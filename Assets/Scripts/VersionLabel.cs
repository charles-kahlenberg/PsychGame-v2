using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The build's version (Player Settings > Version, Application.version) in
// small faint type in the bottom-left corner, on every screen for every
// group, over everything (the menu intro, fades and prompts included), so
// screenshots and bug reports say which build they came from.
public static class VersionLabel
{
    private const float Size = 9f, Margin = 4f; // canvas units, at the scenes' 800 x 450
    private static readonly Color Ink = new Color(0.5f, 0.5f, 0.5f, 0.7f); // grey shows on light and dark screens

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("VersionLabel");
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // above everything
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 450f);
        scaler.matchWidthOrHeight = 1f;

        var label = new GameObject("Version", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        var rt = label.rectTransform;
        rt.SetParent(go.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(Margin, Margin);
        rt.sizeDelta = new Vector2(200f, Size * 2f);
        if (TextBoxTheme.Regular != null) label.font = TextBoxTheme.Regular;
        label.text = "v" + Application.version;
        label.fontSize = Size;
        label.color = Ink;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.raycastTarget = false; // never in the way of a click
    }
}
