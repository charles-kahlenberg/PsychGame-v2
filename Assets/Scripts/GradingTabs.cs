using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Feature.GradingTabs (with ThemedTextBoxes): the grading screen leads with
// Brainy's feedback and score. The scenario and the player's response, which
// they've just read and written, wait behind two tabs along the top; clicking
// one opens its text under the tabs and the feedback slides down to make
// room, and clicking it again, Esc, or a click anywhere else puts it away.
// Every panel fits its text, so the background shows below them; the
// feedback scrolls once it reaches the bottom. TextBoxTheme lays out the rest.
public class GradingTabs : MonoBehaviour
{
    private const float TabHeight = 30f;
    private const float TabGap = 8f;
    private const float TabPadX = 16f;
    private const float TabFontSize = 13f;
    private const float MinPanelHeight = 48f;
    private const float TabPanelShare = 0.45f; // of the room, so the feedback always keeps some

    private class Tab
    {
        public string name;
        public RectTransform chip, panel;
        public TMP_Text label;
    }

    private Tab[] _tabs;
    private TextPanel _feedback;
    private float _top, _bottom, _feedbackTop; // down from the screen's top

    // Builds the tabs on a full-screen layer over the canvas (so they slide
    // in and out as one) and the feedback panel under them. `right` is how
    // far in from the right edge the panels stop.
    public static void Build(RectTransform canvas, TMP_Text scenario, TMP_Text response, TMP_Text feedback, float right)
    {
        var row = NewRect("GradingTabs", canvas);
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.one;
        row.offsetMin = row.offsetMax = Vector2.zero;

        float margin = TextBoxTheme.ScreenMargin;
        var tabs = row.gameObject.AddComponent<GradingTabs>();
        tabs._top = tabs._feedbackTop = margin + TabHeight + TabGap;
        tabs._bottom = canvas.rect.height - margin;
        float room = tabs._bottom - tabs._top;

        tabs._feedback = TextBoxTheme.LeftPanel(canvas, feedback, tabs._top, right, MinPanelHeight, room);
        float x = margin;
        tabs._tabs = new[]
        {
            tabs.Add(row, "Scenario", scenario, tabs._top, right, room * TabPanelShare, ref x),
            tabs.Add(row, "Your Response", response, tabs._top, right, room * TabPanelShare, ref x),
        };
    }

    // The feedback follows the open panel's bottom down (and back up), and
    // can grow only as far as the bottom of the screen.
    void LateUpdate()
    {
        if (_feedback == null) return;
        Tab open = Array.Find(_tabs, t => t.panel.gameObject.activeSelf);
        float target = _top + (open != null ? open.panel.rect.height + TabGap : 0f);
        _feedbackTop = Mathf.Lerp(_feedbackTop, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));

        var rt = (RectTransform)_feedback.transform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -_feedbackTop); // x is GradingTransition's to slide
        _feedback.MaxHeight = _bottom - _feedbackTop;
    }

    private Tab Add(RectTransform row, string name, TMP_Text text, float top, float right, float maxHeight, ref float x)
    {
        var tab = new Tab { name = name };
        tab.panel = (RectTransform)TextBoxTheme.LeftPanel(row, text, top, right, MinPanelHeight, maxHeight).transform;

        float margin = TextBoxTheme.ScreenMargin;
        tab.chip = NewRect(name.Replace(" ", "") + "Tab", row); // named for the click log
        TextPanel.AddPaper(tab.chip);

        var labelRect = NewRect("Label", tab.chip);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        tab.label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        TextBoxTheme.StyleBodyText(tab.label, TextBoxTheme.SemiBold, TabFontSize);
        tab.label.color = TextBoxTheme.Accent;
        tab.label.alignment = TextAlignmentOptions.Center;
        tab.label.textWrappingMode = TextWrappingModes.NoWrap;
        tab.label.raycastTarget = false;

        float width = Mathf.Max(tab.label.GetPreferredValues("Show " + name).x,
                                tab.label.GetPreferredValues("Hide " + name).x) + 2f * TabPadX;
        tab.chip.anchorMin = tab.chip.anchorMax = tab.chip.pivot = new Vector2(0f, 1f);
        tab.chip.sizeDelta = new Vector2(width, TabHeight);
        tab.chip.anchoredPosition = new Vector2(x, -margin);
        x += width + TabGap;

        var button = tab.chip.gameObject.AddComponent<Button>();
        button.targetGraphic = tab.chip.Find("Paper").GetComponent<Image>();
        button.onClick.AddListener(() => Toggle(tab));
        if (TestGroups.IsEnabled(Feature.HoverAnimations)) tab.chip.gameObject.AddComponent<ButtonHoverPop>();

        Show(tab, false);
        return tab;
    }

    private void Toggle(Tab tab)
    {
        bool open = !tab.panel.gameObject.activeSelf;
        foreach (Tab t in _tabs) Show(t, false);
        Show(tab, open);
    }

    private static void Show(Tab tab, bool open)
    {
        tab.panel.gameObject.SetActive(open);
        tab.label.text = (open ? "Hide " : "Show ") + tab.name;
    }

    // Clicks on the tabs are left to their buttons, so this never fights a toggle.
    void Update()
    {
        Tab open = Array.Find(_tabs, t => t.panel.gameObject.activeSelf);
        if (open == null) return;

        bool clickedAway = (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
                           !Over(open.panel) && !Array.Exists(_tabs, t => Over(t.chip));
        if (clickedAway || Input.GetKeyDown(KeyCode.Escape)) Show(open, false);
    }

    private static bool Over(RectTransform rt) =>
        RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null); // overlay canvas

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.gameObject.layer = parent.gameObject.layer;
        rt.SetParent(parent, false);
        return rt;
    }
}
