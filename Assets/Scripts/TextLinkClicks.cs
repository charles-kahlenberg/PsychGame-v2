using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Clicks on a TextMeshPro text's <link="id"> tags, e.g. the dictionary's
// index of terms. The text needs raycastTarget on.
public class TextLinkClicks : MonoBehaviour, IPointerClickHandler
{
    public System.Action<string> Clicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        var text = GetComponent<TMP_Text>();
        int link = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (link >= 0) Clicked?.Invoke(text.textInfo.linkInfo[link].GetLinkID());
    }
}
