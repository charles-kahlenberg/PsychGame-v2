using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class OverlayClickCloser : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [Tooltip("The speech bubble container (the white rounded bubble).")]
    public RectTransform bubbleContainer;

    [Tooltip("The overlay GameObject that dims the screen. If left empty, this GameObject will be used.")]
    public GameObject hintOverlay;

    [Tooltip("The TMP text inside the bubble (optional — used to clear text on close).")]
    public TMP_Text hintText;

    [Tooltip("Clicks on these don't close the overlay either (group 2 keeps the response box usable).")]
    public RectTransform[] keepOpenWhenClicked;

    private EventSystem _eventSystem;
    private int _openedFrame = -1;

    void Awake()
    {
        _eventSystem = EventSystem.current;
        if (hintOverlay == null) hintOverlay = gameObject; // default to this object
    }

    // When a quick click's press and release land in the same frame, the
    // button it hit can open this overlay on release, and the press would
    // then read as a click outside the bubble and close it again at once.
    void OnEnable()
    {
        _openedFrame = Time.frameCount;
    }

    void Update()
    {
        if (!hintOverlay || !hintOverlay.activeInHierarchy) return;
        if (Time.frameCount == _openedFrame) return;

        // Close on Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseHint();
            return;
        }

        // Close on click outside
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (_eventSystem == null)
            {
                CloseHint(); // be safe if no EventSystem found
                return;
            }

            var data = new PointerEventData(_eventSystem) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            _eventSystem.RaycastAll(data, results);

            bool clickedInsideBubble = false;

            foreach (var r in results)
            {
                if (bubbleContainer != null && r.gameObject != null &&
                    (r.gameObject == bubbleContainer.gameObject || r.gameObject.transform.IsChildOf(bubbleContainer)))
                {
                    clickedInsideBubble = true;
                    break;
                }
            }

            if (!clickedInsideBubble && !ClickedOnKeepOpen(Input.mousePosition))
                CloseHint();
        }
    }

    private bool ClickedOnKeepOpen(Vector2 screenPoint)
    {
        if (keepOpenWhenClicked == null) return false;

        foreach (var keep in keepOpenWhenClicked)
        {
            if (keep != null && keep.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(keep, screenPoint, null))
                return true;
        }
        return false;
    }

    public void CloseHint()
    {
        if (bubbleContainer) bubbleContainer.gameObject.SetActive(false);
        if (hintOverlay) hintOverlay.SetActive(false);
        if (hintText) hintText.text = string.Empty;
    }
}
