using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the response box's scrollbar only while the answer is too long to fit.
public class ScrollbarWhenOverflowing : MonoBehaviour
{
    private TMP_InputField _input;
    private Scrollbar _scrollbar;

    public void Init(TMP_InputField input, Scrollbar scrollbar)
    {
        _input = input;
        _scrollbar = scrollbar;
    }

    private void LateUpdate()
    {
        if (_input == null || _scrollbar == null || _input.textViewport == null) return;
        bool overflowing = _input.textComponent.preferredHeight > _input.textViewport.rect.height + 0.5f;
        if (_scrollbar.gameObject.activeSelf != overflowing) _scrollbar.gameObject.SetActive(overflowing);
    }
}
