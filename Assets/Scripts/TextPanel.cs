using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A themed text box built on an existing object: shadow, paper panel, speech
// tail, optional name tag, and a scroll view holding the existing text. It
// sizes itself to the text between a minimum and maximum height and shows a
// scrollbar when the text runs past the maximum.
public class TextPanel : MonoBehaviour
{
    public enum TailSide { Bottom, Left }

    public const float TailLength = 23.5f;  // how far the tail reaches out from the panel
    public const float TailDrop = 18f;      // how far the left tail's tip sits below where it leaves the panel
    public const float ScrollbarGutter = 9f;
    private const float PadX = 16f, PadTop = 12f, PadBottom = 13f, NameTagExtra = 5f;
    private const float Shadow = 4f, ShadowDrop = 3f;

    public ScrollRect Scroll { get; private set; }

    private RectTransform _panel, _viewport, _content;
    private TMP_Text _text;
    private Scrollbar _scrollbar;
    private float _minHeight, _maxHeight, _padTop;
    private string _fittedText;
    private float _fittedWidth = -1f, _targetHeight;
    private bool _snap = true;
    private TextMeshProUGUI _nameTag;
    private bool _nameTagSized;

    public static TextPanel Build(RectTransform panel, TMP_Text text, float minHeight, float maxHeight, string nameTag)
    {
        AddPaper(panel);

        var p = panel.gameObject.AddComponent<TextPanel>();
        p._panel = panel;
        p._text = text;
        p._minHeight = minHeight;
        p._maxHeight = maxHeight;
        p._padTop = PadTop + (nameTag != null ? NameTagExtra : 0f);

        p._viewport = NewChild("Viewport", panel);
        p._viewport.anchorMin = Vector2.zero;
        p._viewport.anchorMax = Vector2.one;
        p._viewport.offsetMin = new Vector2(PadX, PadBottom);
        p._viewport.offsetMax = new Vector2(-PadX, -p._padTop);
        p._viewport.gameObject.AddComponent<RectMask2D>();

        p._content = (RectTransform)text.transform;
        p._content.SetParent(p._viewport, false);
        p._content.anchorMin = new Vector2(0f, 1f);
        p._content.anchorMax = new Vector2(1f, 1f);
        p._content.pivot = new Vector2(0.5f, 1f);
        p._content.anchoredPosition = Vector2.zero;
        p._content.sizeDelta = new Vector2(0f, 20f);
        p._content.localScale = Vector3.one;
        p._content.localRotation = Quaternion.identity;
        text.raycastTarget = false;

        p._scrollbar = MakeScrollbar(panel, p._padTop, PadBottom);

        p.Scroll = panel.gameObject.AddComponent<ScrollRect>();
        p.Scroll.horizontal = false;
        p.Scroll.vertical = true;
        p.Scroll.movementType = ScrollRect.MovementType.Clamped;
        p.Scroll.scrollSensitivity = 18f;
        p.Scroll.viewport = p._viewport;
        p.Scroll.content = p._content;

        // The scrollbar is driven here rather than by the ScrollRect, which
        // would show it whenever the box is mid-resize; this shows it only
        // when the text really runs past the box's cap.
        p._scrollbar.onValueChanged.AddListener(v => p.Scroll.verticalNormalizedPosition = v);
        p.Scroll.onValueChanged.AddListener(_ => p.SyncScrollbar());

        if (nameTag != null) p._nameTag = AddNameTag(panel, nameTag);
        return p;
    }

    // A letter-by-letter reveal that lays the whole text out first and only
    // uncovers it, so words don't jump lines mid-type and a panel is its
    // final size from the first letter. `skip` finishes it at once (a click).
    public static System.Collections.IEnumerator Reveal(TMP_Text text, string s, float delay, System.Func<bool> skip)
    {
        text.text = s;
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;

        float start = Time.time;
        while (!skip())
        {
            int shown = Mathf.FloorToInt((Time.time - start) / delay) + 1;
            if (shown >= total) break;
            text.maxVisibleCharacters = shown;
            yield return null;
        }

        text.maxVisibleCharacters = 99999; // TMP's default: no limit
    }

    // `along` is how far along the edge (0-1) the tail sits; `offset` nudges
    // it from there in canvas units, e.g. a fixed distance below the top.
    public void AddTail(TailSide side, float along, float offset = 0f)
    {
        var tail = NewChild("Tail", _panel);
        var image = tail.gameObject.AddComponent<Image>();
        image.sprite = PanelSprites.Tail;
        image.raycastTarget = false;
        tail.sizeDelta = new Vector2(PanelSprites.Tail.rect.width, PanelSprites.Tail.rect.height) / PanelSprites.Density;
        tail.pivot = PanelSprites.TailBase;

        // The tail's base overlaps the panel's edge line by exactly its width,
        // so its paper covers the edge and the two outlines meet cleanly.
        if (side == TailSide.Bottom)
        {
            tail.anchorMin = tail.anchorMax = new Vector2(along, 0f);
            tail.anchoredPosition = new Vector2(offset, PanelSprites.EdgeWidth);
        }
        else
        {
            tail.anchorMin = tail.anchorMax = new Vector2(0f, along);
            tail.anchoredPosition = new Vector2(PanelSprites.EdgeWidth, offset);
            tail.localRotation = Quaternion.Euler(0f, 0f, -90f); // points left, tip a little low
        }

        // Above the paper, beneath the text.
        tail.SetSiblingIndex(_panel.Find("Paper").GetSiblingIndex() + 1);
    }

    // Draws the panel as paper with a soft shadow. Both are children at the
    // bottom of the panel, since a child's shadow would otherwise cover its
    // parent's own image. The panel's own Image stays as an invisible click
    // target: clicks on it still log under its name, and it catches the
    // mouse wheel.
    public static void AddPaper(RectTransform panel)
    {
        var own = panel.GetComponent<Image>();
        if (own == null) own = panel.gameObject.AddComponent<Image>();
        own.sprite = null;
        own.color = new Color(1f, 1f, 1f, 0f);
        own.raycastTarget = true;
        own.canvasRenderer.cullTransparentMesh = false; // a culled graphic can't be clicked or scrolled

        var paper = NewChild("Paper", panel);
        paper.SetAsFirstSibling();
        Stretch(paper);
        var paperImage = paper.gameObject.AddComponent<Image>();
        paperImage.sprite = PanelSprites.Panel;
        paperImage.type = Image.Type.Sliced;
        paperImage.pixelsPerUnitMultiplier = PanelSprites.Density;
        paperImage.raycastTarget = false;

        AddShadow(panel);
    }

    private static void AddShadow(RectTransform panel)
    {
        var shadow = NewChild("Shadow", panel);
        shadow.SetAsFirstSibling();
        shadow.anchorMin = Vector2.zero;
        shadow.anchorMax = Vector2.one;
        shadow.offsetMin = new Vector2(-Shadow, -Shadow - ShadowDrop);
        shadow.offsetMax = new Vector2(Shadow, Shadow - ShadowDrop);
        var image = shadow.gameObject.AddComponent<Image>();
        image.sprite = PanelSprites.Shadow;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = PanelSprites.Density;
        image.color = new Color(0f, 0f, 0f, 0.35f);
        image.raycastTarget = false;
    }

    // A slim pill scrollbar along the panel's right edge.
    public static Scrollbar MakeScrollbar(RectTransform panel, float topInset, float bottomInset)
    {
        var bar = NewChild("Scrollbar", panel);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(4f, -(topInset + bottomInset));
        bar.anchoredPosition = new Vector2(-6f, (bottomInset - topInset) / 2f);
        var track = bar.gameObject.AddComponent<Image>();
        track.sprite = PanelSprites.Pill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = PanelSprites.Density;
        track.color = new Color(TextBoxTheme.Edge.r, TextBoxTheme.Edge.g, TextBoxTheme.Edge.b, 0.14f);

        var handle = NewChild("Handle", bar);
        Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = PanelSprites.Pill;
        handleImage.type = Image.Type.Sliced;
        handleImage.pixelsPerUnitMultiplier = PanelSprites.Density;
        handleImage.color = new Color(TextBoxTheme.Accent.r, TextBoxTheme.Accent.g, TextBoxTheme.Accent.b, 0.8f);

        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        var colors = scrollbar.colors;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        scrollbar.colors = colors;
        bar.gameObject.SetActive(false);
        return scrollbar;
    }

    private static TextMeshProUGUI AddNameTag(RectTransform panel, string name)
    {
        var tag = NewChild("NameTag", panel);
        tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
        tag.pivot = new Vector2(0f, 0.5f);
        tag.anchoredPosition = new Vector2(14f, 0f);
        var image = tag.gameObject.AddComponent<Image>();
        image.sprite = PanelSprites.Pill;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = PanelSprites.Density / 4f; // fully round ends at this height
        image.color = TextBoxTheme.Accent;
        image.raycastTarget = false;

        var label = NewChild("Label", tag);
        Stretch(label);
        var text = label.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TextBoxTheme.SemiBold;
        text.fontSize = 11f;
        text.color = TextBoxTheme.Paper;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = name;
        tag.sizeDelta = new Vector2(60f, 19f); // widened to fit once it's first shown
        return text;
    }

    private void OnEnable()
    {
        _snap = true; // open at the right size instead of growing from the last one
    }

    private void LateUpdate()
    {
        // Text can only be measured once it's active, so the name tag is
        // fitted to its label the first time the panel shows.
        if (_nameTag != null && !_nameTagSized)
        {
            _nameTagSized = true;
            var tag = (RectTransform)_nameTag.transform.parent;
            tag.sizeDelta = new Vector2(_nameTag.GetPreferredValues(_nameTag.text).x + 18f, tag.sizeDelta.y);
        }

        float width = _panel.rect.width - 2f * PadX;
        if (_text.text != _fittedText || !Mathf.Approximately(width, _fittedWidth))
            Fit(width);

        float current = _panel.rect.height;
        if (_snap || Mathf.Abs(current - _targetHeight) < 0.5f)
        {
            if (current != _targetHeight) SetHeight(_targetHeight);
            _snap = false;
        }
        else
        {
            SetHeight(Mathf.Lerp(current, _targetHeight, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f)));
        }

        SyncScrollbar();
    }

    private void SyncScrollbar()
    {
        if (!_scrollbar.gameObject.activeSelf) return;
        float content = _content.rect.height;
        _scrollbar.size = content > 0f ? Mathf.Clamp01(_viewport.rect.height / content) : 1f;
        _scrollbar.SetValueWithoutNotify(Scroll.verticalNormalizedPosition);
    }

    private void Fit(float width)
    {
        bool newText = _text.text != _fittedText;
        _fittedText = _text.text;
        _fittedWidth = width;

        string s = _fittedText ?? "";
        float textHeight = s.Length == 0 ? 0f : _text.GetPreferredValues(s, width, 0f).y;
        bool overflowing = _padTop + textHeight + PadBottom > _maxHeight;
        if (overflowing)
            textHeight = _text.GetPreferredValues(s, width - ScrollbarGutter, 0f).y;

        _viewport.offsetMax = new Vector2(-(PadX + (overflowing ? ScrollbarGutter : 0f)), -_padTop);
        _content.sizeDelta = new Vector2(0f, textHeight);
        _scrollbar.gameObject.SetActive(overflowing);
        _targetHeight = Mathf.Clamp(_padTop + textHeight + PadBottom, _minHeight, _maxHeight);

        if (newText) Scroll.verticalNormalizedPosition = 1f; // new text starts at its top
    }

    private void SetHeight(float height)
    {
        _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
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
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
