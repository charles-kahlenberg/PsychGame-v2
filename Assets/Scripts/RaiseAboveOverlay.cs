using UnityEngine;

// Group 2 (Feature.ThemedTextBoxes): while the object this sits on (Brainy's
// bubble) is open, keeps `target` (the response box) drawn above the dimmed
// hint overlay, so it stays readable and clickable; puts it back in its
// original place in the hierarchy when the bubble closes.
public class RaiseAboveOverlay : MonoBehaviour
{
    public RectTransform target;
    public Transform overlay;

    private int _home = -1;

    private void LateUpdate()
    {
        if (target == null || overlay == null || !overlay.gameObject.activeInHierarchy) return;
        if (target.parent != overlay.parent) return;

        int overlayIndex = overlay.GetSiblingIndex();
        int targetIndex = target.GetSiblingIndex();
        if (targetIndex < overlayIndex)
        {
            if (_home < 0) _home = targetIndex;
            target.SetSiblingIndex(overlayIndex); // just above the overlay, still below this bubble
        }
    }

    private void OnDisable()
    {
        if (target != null && _home >= 0) target.SetSiblingIndex(_home);
        _home = -1;
    }
}
