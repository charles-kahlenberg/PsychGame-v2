using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Feature.Dictionary (groups 2 and 3): every term and its definition, in a
// book that sits closed in the bottom-right corner of the response screen
// (Resources/DictionaryBook; FitBesideHand shrinks it on narrow screens, and
// it pops on hover like the other buttons). Clicking it opens the book
// (DictionaryBookOpen) and slowly carries it up to the middle of the screen,
// where its pages hold the dictionary: on the left a search box over the
// index of terms, A to Z, each with its areas' icons (CardArt) on its left; on the right the chosen term and its definition.
// Esc or a click off the book carries it back to its corner.
public class TermDictionary : MonoBehaviour
{
    private const string IconPath = "DictionaryBook", OpenIconPath = "DictionaryBookOpen";
    private const float BookHeight = 110f;     // the closed book, in canvas units (the canvas is 450 tall), when there's room
    private const float OpenHeight = 400f;     // the open book in the middle, if the screen's wide enough
    private const float OpenMargin = 12f;      // ...otherwise it fits the width, this far from each side
    private const float OpenTime = 0.7f, CloseTime = 0.45f, FadeTime = 0.2f;
    private const float SearchHeight = 28f, SearchGap = 8f;
    private const float IndexFontSize = 13f, EntryFontSize = 14f;
    private const float IconHeight = 18f, IconGap = 3f; // the index's area icons

    // The pages' writing areas in DictionaryBookOpen, as fractions of the picture (from its bottom left).
    private static readonly Rect LeftPage = Rect.MinMaxRect(0.085f, 0.16f, 0.46f, 0.877f);
    private static readonly Rect RightPage = Rect.MinMaxRect(0.565f, 0.16f, 0.925f, 0.877f);

    private GameManager _game;
    private RectTransform _canvas, _book, _page;
    private Camera _camera;
    private Image _icon, _dim;
    private Sprite _open;
    private CanvasGroup _contents;
    private TMP_InputField _search;
    private TMP_Text _index, _entry;
    private List<string> _terms;                               // A to Z, read on the first open (GameManager loads them in Start)
    private readonly List<string> _matches = new List<string>(); // what the index shows
    private string _chosen;
    private readonly Dictionary<string, Sprite[]> _icons = new Dictionary<string, Sprite[]>(); // each term's area icons
    private readonly List<Image> _iconImages = new List<Image>();
    private bool _closing;

    // From GameManager.Awake, once the hand is raised.
    public static void Build(GameManager game)
    {
        Canvas canvas = game.responseInput != null ? game.responseInput.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return; // not the response screen (the save screen has a GameManager too)

        Sprite icon = Resources.Load<Sprite>(IconPath);
        Sprite open = Resources.Load<Sprite>(OpenIconPath);
        if (icon == null || open == null)
        {
            Debug.LogWarning($"[TermDictionary] Resources/{IconPath} or {OpenIconPath} is missing; no dictionary.");
            return;
        }

        canvas = canvas.rootCanvas;
        var c = (RectTransform)canvas.transform;
        var book = NewChild("DictionaryButton", c);
        var back = c.Find("BackButton");
        if (back != null) book.SetSiblingIndex(back.GetSiblingIndex()); // with the other buttons, under the overlays
        var image = book.gameObject.AddComponent<Image>();
        image.sprite = icon;
        FitBesideHand.Pin(book, new Vector2(BookHeight * icon.rect.width / icon.rect.height, BookHeight), right: true);

        var d = book.gameObject.AddComponent<TermDictionary>();
        d._game = game;
        d._canvas = c;
        d._camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        d._book = book;
        d._icon = image;
        d._open = open;
        book.gameObject.AddComponent<Button>().onClick.AddListener(d.Open);
        d.BuildPage();
    }

    // -------------------- BUILDING --------------------

    // A dimmed screen holding the open book, with the pages' contents on it.
    private void BuildPage()
    {
        var overlay = NewChild("DictionaryOverlay", _canvas);
        Stretch(overlay);
        _dim = overlay.gameObject.AddComponent<Image>();
        _dim.color = TextBoxTheme.OverlayTint;
        overlay.gameObject.SetActive(false);

        _page = NewChild("DictionaryPage", overlay);
        _page.gameObject.AddComponent<Image>().sprite = _open;

        var contents = NewChild("Pages", _page);
        Stretch(contents);
        _contents = contents.gameObject.AddComponent<CanvasGroup>();

        // Left page: the search box, and the index under it.
        var left = Area(contents, "LeftPage", LeftPage);

        var search = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        search.name = "DictionarySearch";
        foreach (Transform t in search.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = left.gameObject.layer;
        var searchRect = (RectTransform)search.transform;
        searchRect.SetParent(left, false);
        TopStrip(searchRect, 0f, SearchHeight);
        _search = search.GetComponent<TMP_InputField>();
        TextBoxTheme.StyleLineInput(_search);
        ((TMP_Text)_search.placeholder).text = "Search...";
        _search.onValueChanged.AddListener(_ => ShowIndex());

        var index = NewChild("Index", left);
        Stretch(index);
        index.offsetMax = new Vector2(0f, -(SearchHeight + SearchGap));
        _index = ScrollingText(index, IndexFontSize);
        _index.raycastTarget = true; // for clicks on the terms
        _index.gameObject.AddComponent<TextLinkClicks>().Clicked = Choose;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        // Right page: the chosen term.
        _entry = NewChild("Entry", Area(contents, "RightPage", RightPage)).gameObject.AddComponent<TextMeshProUGUI>();
        Stretch(_entry.rectTransform);
        TextBoxTheme.StyleBodyText(_entry, TextBoxTheme.Regular, EntryFontSize);
    }

    // A text in a vertical scroll view filling `view`, with a slim scrollbar that shows when it's needed.
    private static TMP_Text ScrollingText(RectTransform view, float fontSize)
    {
        view.gameObject.AddComponent<RectMask2D>();

        var text = NewChild("Text", view).gameObject.AddComponent<TextMeshProUGUI>();
        TextBoxTheme.StyleBodyText(text, TextBoxTheme.Regular, fontSize);
        TopStrip(text.rectTransform, 0f, 0f);
        text.rectTransform.offsetMax = new Vector2(-TextPanel.ScrollbarGutter, 0f);
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollbar = TextPanel.MakeScrollbar(view, 0f, 0f);
        scrollbar.gameObject.SetActive(true);

        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 18f;
        scroll.viewport = view;
        scroll.content = text.rectTransform;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return text;
    }

    // -------------------- OPENING AND CLOSING --------------------

    private void Open()
    {
        if (_terms == null)
        {
            _terms = new List<string>(_game.Definitions.Keys);
            _terms.Sort(System.StringComparer.OrdinalIgnoreCase);
            LoadIcons();
            ShowIndex();
        }

        _closing = false;
        _dim.color = new Color(_dim.color.r, _dim.color.g, _dim.color.b, 0f);
        _page.parent.SetAsLastSibling();
        _page.parent.gameObject.SetActive(true);
        _contents.alpha = 0f;
        _contents.blocksRaycasts = false;

        Rect from = ClosedRect();
        _icon.enabled = false; // the book's now the one flying to the middle
        float aspect = _open.rect.width / _open.rect.height;
        float height = Mathf.Min(OpenHeight, (_canvas.rect.width - 2f * OpenMargin) / aspect);
        Rect to = new Rect(-height * aspect / 2f, -height / 2f, height * aspect, height);

        Fly(from, to, OpenTime, () =>
        {
            _contents.blocksRaycasts = true;
            LeanTween.alphaCanvas(_contents, 1f, FadeTime);
            _search.ActivateInputField();
        });
    }

    private void Close()
    {
        _closing = true;
        _contents.blocksRaycasts = false;
        LeanTween.alphaCanvas(_contents, 0f, FadeTime * 0.5f);

        Rect from = PixelButton.LocalRect(_canvas, _page);
        Fly(from, ClosedRect(), CloseTime, () =>
        {
            _page.parent.gameObject.SetActive(false);
            _icon.enabled = true;
            _closing = false;
        });
    }

    // Where the open book sits when it's in the corner: as tall as the
    // closed one, level with its bottom and right edges.
    private Rect ClosedRect()
    {
        Rect closed = PixelButton.LocalRect(_canvas, _book);
        float width = closed.height * _open.rect.width / _open.rect.height;
        return new Rect(closed.xMax - width, closed.yMin, width, closed.height);
    }

    // Carries the open book between two rectangles (canvas space), dimming
    // the screen as it nears the middle and undimming it on the way back.
    private void Fly(Rect from, Rect to, float time, System.Action done)
    {
        LeanTween.cancel(_page.gameObject);
        float fromDim = _dim.color.a, toDim = _closing ? 0f : TextBoxTheme.OverlayTint.a;
        _page.anchoredPosition = from.center; // not where it was left, for the frame before the tween's first step
        _page.sizeDelta = from.size;
        LeanTween.value(_page.gameObject, 0f, 1f, time).setEaseInOutCubic()
            .setOnUpdate(t =>
            {
                _page.anchoredPosition = Vector2.Lerp(from.center, to.center, t);
                _page.sizeDelta = Vector2.Lerp(from.size, to.size, t);
                var c = _dim.color;
                c.a = Mathf.Lerp(fromDim, toDim, t);
                _dim.color = c;
            })
            .setOnComplete(done);
    }

    // Esc, or a click anywhere off the open book, carries it back.
    private void Update()
    {
        if (!_page.parent.gameObject.activeSelf || _closing) return;
        if (Input.GetKeyDown(KeyCode.Escape) ||
            (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(_page, Input.mousePosition, _camera)))
            Close();
    }

    // -------------------- THE PAGES --------------------

    // The index: every term containing what's typed, the chosen one in the
    // accent colour. The first match is chosen if the last choice is gone.
    private void ShowIndex()
    {
        if (_terms == null) return;
        string query = _search.text.Trim();

        _matches.Clear();
        foreach (string term in _terms)
            if (term.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) _matches.Add(term);
        if (!_matches.Contains(_chosen)) _chosen = _matches.Count > 0 ? _matches[0] : null;

        if (_matches.Count == 0)
        {
            _index.text = $"No terms match \"<noparse>{query}</noparse>\".";
        }
        else
        {
            string accent = ColorUtility.ToHtmlStringRGB(TextBoxTheme.Accent);
            var sb = new StringBuilder();
            for (int i = 0; i < _matches.Count; i++)
            {
                string term = _matches[i] == _chosen ? $"<color=#{accent}><b>{_matches[i]}</b></color>" : _matches[i];
                sb.Append($"<link=\"{i}\">{term}</link>\n");
            }
            _index.text = sb.ToString().TrimEnd();
        }

        _entry.text = _chosen == null ? "" : $"<size=130%><b>{_chosen}</b></size>\n\n{_game.Definitions[_chosen]}";
    }

    private void Choose(string link)
    {
        if (!int.TryParse(link, out int i) || i >= _matches.Count) return;
        _chosen = _matches[i];
        ShowIndex();
    }

    // -------------------- AREA ICONS --------------------

    // Every term's icons, and a column left of the index wide enough for the most any term has.
    private void LoadIcons()
    {
        CardArt art = CardArt.Load();
        float column = 0f;
        foreach (string term in _terms)
        {
            Sprite[] icons = art != null ? art.IconsFor(TermAreas.Get(term)) : new Sprite[0];
            _icons[term] = icons;
            float width = 0f;
            foreach (Sprite icon in icons) width += IconWidth(icon) + IconGap;
            column = Mathf.Max(column, width);
        }
        _index.margin = new Vector4(column, 0f, 0f, 0f);
    }

    private static float IconWidth(Sprite icon) => IconHeight * icon.rect.width / icon.rect.height;

    // The index's lines only have their final places once TextMeshPro has
    // laid it out (it re-lays it out whenever the text or its width changes).
    // That happens inside the canvas rebuild, where touching the icon Images
    // is an error, so they're placed next frame instead.
    private bool _iconsDirty;

    private void OnTextChanged(Object text)
    {
        if (text == _index) _iconsDirty = true;
    }

    private void LateUpdate()
    {
        if (!_iconsDirty) return;
        _iconsDirty = false;
        PlaceIcons();
    }

    // Each listed term's icons, left to right in the column, level with its
    // (first) line. Images are reused, and those left over are hidden.
    private void PlaceIcons()
    {
        TMP_TextInfo info = _index.textInfo;
        float left = _index.rectTransform.rect.xMin;
        int used = 0;
        for (int i = 0; i < info.linkCount && i < _matches.Count; i++)
        {
            TMP_LineInfo line = info.lineInfo[info.characterInfo[info.linkInfo[i].linkTextfirstCharacterIndex].lineNumber];
            float x = left;
            foreach (Sprite icon in _icons[_matches[i]])
            {
                if (used == _iconImages.Count) _iconImages.Add(NewIcon());
                Image image = _iconImages[used++];
                image.sprite = icon;
                image.rectTransform.sizeDelta = new Vector2(IconWidth(icon), IconHeight);
                image.rectTransform.anchoredPosition = new Vector2(x, (line.ascender + line.descender) / 2f);
                image.gameObject.SetActive(true);
                x += IconWidth(icon) + IconGap;
            }
        }
        for (int i = used; i < _iconImages.Count; i++) _iconImages[i].gameObject.SetActive(false);
    }

    // Placed in the index text's own space (from its pivot), so it scrolls with it.
    private Image NewIcon()
    {
        var rt = NewChild("AreaIcon", _index.transform);
        rt.anchorMin = rt.anchorMax = _index.rectTransform.pivot;
        rt.pivot = new Vector2(0f, 0.5f);
        var image = rt.gameObject.AddComponent<Image>();
        image.raycastTarget = false; // clicks go to the term
        return image;
    }

    private void OnDestroy()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    // -------------------- HELPERS --------------------

    // A child covering `area` of its parent, given as fractions of it.
    private static RectTransform Area(RectTransform parent, string name, Rect area)
    {
        var rt = NewChild(name, parent);
        rt.anchorMin = area.min;
        rt.anchorMax = area.max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Full width along the top of its parent, `top` down from its top edge.
    private static void TopStrip(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
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
