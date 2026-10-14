using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Clicks on, and the pointer over, a TextMeshPro text's <link="id"> tags,
// e.g. the dictionary's list of the hand. The text needs raycastTarget on.
public class TextLinkClicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action<string> Clicked;
    public System.Action<string> Hovered; // the link under the pointer whenever it changes; null off the links

    private TMP_Text _text;
    private bool _inside;
    private Camera _camera;
    private string _hovered;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        string link = LinkAt(eventData.position, eventData.pressEventCamera);
        if (link != null) Clicked?.Invoke(link);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _inside = true;
        _camera = eventData.enterEventCamera;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _inside = false;
        SetHovered(null);
    }

    private void Update()
    {
        if (_inside) SetHovered(LinkAt(Input.mousePosition, _camera));
    }

    private void SetHovered(string link)
    {
        if (link == _hovered) return;
        _hovered = link;
        Hovered?.Invoke(link);
    }

    private string LinkAt(Vector2 position, Camera camera)
    {
        int link = TMP_TextUtilities.FindIntersectingLink(_text, position, camera);
        return link >= 0 ? _text.textInfo.linkInfo[link].GetLinkID() : null;
    }
}
