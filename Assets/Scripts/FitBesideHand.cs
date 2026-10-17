using UnityEngine;

// The response screen's buttons beside the hand (Menu in the bottom-left
// corner, the dictionary's book just right of the cards): pinned to the corner or against
// the hand, and shrunk to the room between that screen edge and the hand, so
// a narrow screen neither cuts them off nor runs them under the cards. The canvas keeps its height and
// changes width with the window, so they refit whenever the width changes.
public class FitBesideHand : MonoBehaviour
{
    public const float ScreenMargin = 14f;
    private const float HandClearance = 12f; // the card art widens the cards a little, and hovering grows them
    private const float MinScale = 0.4f;     // past this it would be too small to read; it overlaps instead

    private RectTransform _canvas, _rt;
    private Vector2 _fullSize;
    private float _handEdge; // the hand's nearest corner on this side, from the canvas's center
    private bool _right;
    private float _fittedWidth = -1f;

    // Call once the hand is laid out (after GameManager.RaiseHand). `fullSize`
    // is the size it shows at when there's room. `atHand` sets it against the
    // hand instead of in the screen's corner.
    public static void Pin(RectTransform rt, Vector2 fullSize, bool right, bool atHand = false)
    {
        var fit = rt.gameObject.AddComponent<FitBesideHand>();
        fit._canvas = (RectTransform)rt.GetComponentInParent<Canvas>().rootCanvas.transform;
        fit._rt = rt;
        fit._fullSize = fullSize;
        fit._right = right;
        for (int i = 1; i <= 5; i++)
        {
            if (!(fit._canvas.Find($"Card{i}") is RectTransform card)) continue;
            float edge = GameManager.CardExtent(card, 0, min: !right);
            fit._handEdge = right ? Mathf.Max(fit._handEdge, edge) : Mathf.Min(fit._handEdge, edge);
        }

        if (atHand)
        {
            // The cards are anchored at the canvas's center, so this holds at any screen shape.
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(right ? 0f : 1f, 0f);
            rt.anchoredPosition = new Vector2(fit._handEdge + (right ? HandClearance : -HandClearance), ScreenMargin);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(right ? 1f : 0f, 0f);
            rt.anchoredPosition = new Vector2(right ? -ScreenMargin : ScreenMargin, ScreenMargin);
        }
    }

    private void Update()
    {
        Rect canvas = _canvas.rect;
        if (canvas.width == _fittedWidth) return;
        _fittedWidth = canvas.width;

        float room = _right
            ? canvas.xMax - ScreenMargin - (_handEdge + HandClearance)
            : _handEdge - HandClearance - (canvas.xMin + ScreenMargin);
        _rt.sizeDelta = _fullSize * Mathf.Clamp(room / _fullSize.x, MinScale, 1f);
    }
}
