using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Feature.Dictionary (groups 2 and 3): the terms in the player's hand and
// their definitions, in a book that sits closed in the bottom-right corner of the response screen
// (Resources/DictionaryBook; FitBesideHand shrinks it on narrow screens, and
// it pops on hover like the other buttons). Clicking it opens the book
// (DictionaryBookOpen) and slowly carries it up to the middle of the screen,
// where its pages hold the dictionary: on the left the hand, card by card,
// each term with its areas' icons (CardArt) on its left, or "Unknown" for a
// card still face-down; on the right the chosen card's term and definition.
// Esc or a click off the book carries it back to its corner.
public class TermDictionary : MonoBehaviour
{
    private const string IconPath = "DictionaryBook", OpenIconPath = "DictionaryBookOpen";
    private const float BookHeight = 110f;     // the closed book, in canvas units (the canvas is 450 tall), when there's room
    private const float OpenHeight = 400f;     // the open book in the middle, if the screen's wide enough
    private const float OpenMargin = 12f;      // ...otherwise it fits the width, this far from each side
    private const float OpenTime = 0.7f, CloseTime = 0.45f, FadeTime = 0.2f;
    private const float FontSize = 18f, MinFontSize = 11f; // a long hand or definition shrinks to fit its page
    // The hand's list: a term's wrapped lines close together, a clear gap
    // before the next term (both in hundredths of the font size).
    private const float IndexLineSpacing = -8f, IndexTermSpacing = 70f;
    private const float TitleScale = 1.3f, TitleDilate = 0.2f, TitleGap = 12f; // the right page's term, thickened past SemiBold (there's no Bold font)
    private const string UnknownTerm = "Unknown";
    private const float IconHeight = 22f, IconGap = 3f; // the index's area icons

    // The pages' writing areas in DictionaryBookOpen, as fractions of the
    // picture (from its bottom left): below the light band along the pages'
    // curved tops, with a little room (78 of its 324 pixels down; the hand's
    // list 88).
    private static readonly Rect LeftPage = Rect.MinMaxRect(0.085f, 0.16f, 0.46f, 0.728f);
    private static readonly Rect RightPage = Rect.MinMaxRect(0.565f, 0.16f, 0.925f, 0.76f);

    private GameManager _game;
    private RectTransform _canvas, _book, _page;
    private Camera _camera;
    private Image _icon, _dim;
    private Sprite _open;
    private CanvasGroup _contents;
    private TMP_Text _index, _title, _entry;
    private readonly CardBehavior[] _cards = new CardBehavior[5]; // Card1..Card5, in the same order as GameManager.currentCards
    private int _chosen;                                           // which card the right page shows
    private int _hovered = -1;                                     // which card's line the pointer is over
    private Sprite[][] _slotIcons = new Sprite[0][];               // each listed card's area icons (none while face-down)
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
        for (int i = 0; i < d._cards.Length; i++)
            d._cards[i] = c.Find($"Card{i + 1}")?.GetComponent<CardBehavior>();
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

        // Left page: the hand.
        _index = NewChild("Index", Area(contents, "LeftPage", LeftPage)).gameObject.AddComponent<TextMeshProUGUI>();
        Stretch(_index.rectTransform);
        PageText(_index, FontSize);
        _index.lineSpacing = IndexLineSpacing;
        _index.paragraphSpacing = IndexTermSpacing;
        _index.raycastTarget = true; // for clicks on the terms
        var links = _index.gameObject.AddComponent<TextLinkClicks>();
        links.Clicked = Choose;
        links.Hovered = link =>
        {
            _hovered = link != null && int.TryParse(link, out int i) ? i : -1;
            ShowIndex();
        };
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        // Right page: the chosen term, and its definition filling the rest of the page.
        var right = Area(contents, "RightPage", RightPage);
        var column = right.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = TitleGap;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        _title = NewChild("Title", right).gameObject.AddComponent<TextMeshProUGUI>();
        TextBoxTheme.StyleBodyText(_title, TextBoxTheme.SemiBold, FontSize * TitleScale);
        _title.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, TitleDilate); // its own copy of the material, so only the title thickens
        _title.UpdateMeshPadding();

        _entry = NewChild("Definition", right).gameObject.AddComponent<TextMeshProUGUI>();
        PageText(_entry, FontSize);
        var fill = _entry.gameObject.AddComponent<LayoutElement>();
        fill.preferredHeight = 0f; // takes the room left under the title, and shrinks to fit it
        fill.flexibleHeight = 1f;
    }

    // -------------------- OPENING AND CLOSING --------------------

    private void Open()
    {
        // The hand may have been refreshed or turned over since the last
        // look; a card still face-down gives way to the first face-up one.
        if (!Known(_chosen)) _chosen = System.Math.Max(0, System.Array.FindIndex(_cards, card => card != null && card.IsRevealed));
        ShowIndex();

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

    // The hand, card by card, the chosen one in bold in the accent colour; a
    // card still face-down is listed as Unknown, dimmed, with no icons. The
    // one under the pointer is underlined in the accent colour, so the
    // lines read as something to click.
    private void ShowIndex()
    {
        List<string> hand = _game.currentCards;
        if (_chosen >= hand.Count) _chosen = 0;
        string accent = ColorUtility.ToHtmlStringRGB(TextBoxTheme.Accent);
        string dimInk = ColorUtility.ToHtmlStringRGB(TextBoxTheme.Ink) + "99";

        CardArt art = CardArt.Load();
        _slotIcons = new Sprite[hand.Count][];
        float column = 0f;
        var sb = new StringBuilder();
        for (int i = 0; i < hand.Count; i++)
        {
            bool known = Known(i);
            _slotIcons[i] = known && art != null ? art.IconsFor(TermAreas.Get(hand[i])) : new Sprite[0];
            float width = 0f;
            foreach (Sprite icon in _slotIcons[i]) width += IconWidth(icon) + IconGap;
            column = Mathf.Max(column, width);

            string name = known ? hand[i] : $"<i>{UnknownTerm}</i>";
            if (i == _chosen) name = $"<color=#{accent}><b>{name}</b></color>";
            else if (i == _hovered) name = $"<color=#{accent}>{name}</color>";
            else if (!known) name = $"<color=#{dimInk}>{name}</color>";
            if (i == _hovered) name = $"<u>{name}</u>";
            sb.Append($"<link=\"{i}\">{name}</link>\n");
        }
        _index.margin = new Vector4(column, 0f, 0f, 0f); // room on the left for the most icons any card has
        _index.text = sb.ToString().TrimEnd();

        if (hand.Count == 0)
        {
            _title.text = _entry.text = "";
        }
        else if (!Known(_chosen))
        {
            _title.text = UnknownTerm;
            _entry.text = "This card is still face-down. Turn it over in your hand to read it here.";
        }
        else
        {
            _title.text = hand[_chosen];
            _entry.text = _game.Definitions.TryGetValue(hand[_chosen], out string definition) ? definition : "No definition available.";
        }
    }

    // Whether the card in this slot of the hand is face-up.
    private bool Known(int slot) => slot < _cards.Length && _cards[slot] != null && _cards[slot].IsRevealed;

    private void Choose(string link)
    {
        if (!int.TryParse(link, out int i) || i >= _game.currentCards.Count) return;
        _chosen = i;
        ShowIndex();
    }

    // -------------------- AREA ICONS --------------------

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
        for (int i = 0; i < info.linkCount && i < _slotIcons.Length; i++)
        {
            TMP_LineInfo line = info.lineInfo[info.characterInfo[info.linkInfo[i].linkTextfirstCharacterIndex].lineNumber];
            float x = left;
            foreach (Sprite icon in _slotIcons[i])
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

    private static void PageText(TMP_Text text, float size)
    {
        TextBoxTheme.StyleBodyText(text, TextBoxTheme.SemiBold, size); // ink that holds up on the pages' texture
        text.fontSizeMax = size;
        text.fontSizeMin = MinFontSize;
        text.enableAutoSizing = true;
    }

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
