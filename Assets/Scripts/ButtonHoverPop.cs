using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.HoverAnimations (Group 2): a button grows slightly with a little
// overshoot when hovered and settles back when the pointer leaves. It's added
// at runtime to every button in each scene, so no scene needs editing. The
// exceptions already animate on hover: cards and Brainy have their own hover,
// and buttons driven by an Animator hover animation keep that.
public class ButtonHoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverScale = 1.08f;
    private const float Duration = 0.15f;

    private Button _button;
    private Vector3 _restScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.HoverAnimations)) return;
        SceneManager.sceneLoaded += (scene, mode) => AddToButtonsIn(scene);
    }

    private static void AddToButtonsIn(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (!ShouldSkip(button)) button.gameObject.AddComponent<ButtonHoverPop>();
            }
        }
    }

    private static bool ShouldSkip(Button button)
    {
        if (button.GetComponent<ButtonHoverPop>() != null) return true;
        if (button.GetComponent<CardBehavior>() != null || button.GetComponent<BrainBehavior>() != null) return true;
        if (button.GetComponent<Canvas>() != null) return true; // a whole-screen click catcher, not a real button
        return HasAnimatorHover(button);
    }

    // Some buttons use Animation transitions; skip them only when their
    // animator actually animates something (the cards' clips are empty).
    private static bool HasAnimatorHover(Button button)
    {
        if (button.transition != Selectable.Transition.Animation) return false;

        var animator = button.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && !clip.empty) return true;
        }
        return false;
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _restScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_button.interactable) return;

        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, _restScale * HoverScale, Duration).setEaseOutBack();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, _restScale, Duration).setEaseOutQuad();
    }

    // A button hidden mid-hover (e.g. a closing panel) comes back at rest size.
    private void OnDisable()
    {
        LeanTween.cancel(gameObject);
        transform.localScale = _restScale;
    }
}
