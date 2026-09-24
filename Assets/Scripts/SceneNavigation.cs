using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneNavigation : MonoBehaviour
{
    [Header("Optional UI References")]
    public Button continueButton;  // Drag from SplashScene Inspector

    void Start()
    {
        // Only run on SplashScene
        if (continueButton != null)
        {
            bool hasSave = SaveManager.HasSave(0) || SaveManager.HasSave(1) || SaveManager.HasSave(2);
            continueButton.interactable = hasSave;
        }
    }

    public void GoToSplash()
    {
        SceneTransition.Load("SplashScene");
    }

    public void GoToReview()
    {
        SceneTransition.Load("ReviewScene");
    }

    public void GoToSaveSelect()
    {
        SceneTransition.Load("SaveSelectScene");
    }

    public void GoToGame()
    {
        SceneTransition.Load("GameScene");
    }
}
