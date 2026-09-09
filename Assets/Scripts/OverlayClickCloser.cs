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

    [Header("Behavior")]
    [Tooltip("Also close when right-clicking.")]
    public bool closeOnRightClick = true;

    [Tooltip("Also close when pressing Escape.")]
    public bool closeOnEscape = true;

    private EventSystem _eventSystem;

    void Awake()
    {
        _eventSystem = EventSystem.current;
        if (hintOverlay == null) hintOverlay = gameObject; // default to this object
    }

    void Update()
    {
        if (!hintOverlay || !hintOverlay.activeInHierarchy) return;

        // Close on Esc
        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseHint();
            return;
        }

        // Close on click outside
        if (Input.GetMouseButtonDown(0) || (closeOnRightClick && Input.GetMouseButtonDown(1)))
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

            if (!clickedInsideBubble)
                CloseHint();
        }
    }

    public void CloseHint()
    {
        if (bubbleContainer) bubbleContainer.gameObject.SetActive(false);
        if (hintOverlay) hintOverlay.SetActive(false);
        if (hintText) hintText.text = string.Empty;
    }
}
