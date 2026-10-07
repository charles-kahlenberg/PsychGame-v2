using UnityEngine;
using UnityEngine.UI;

public class SplashUI : MonoBehaviour
{
    public Button continueButton;

    void Start()
    {
        // Enable Continue only if a save exists
        bool hasSave = SaveManager.HasSave(0) || SaveManager.HasSave(1) || SaveManager.HasSave(2);
        if (continueButton != null)
            continueButton.interactable = hasSave;
    }

    // NEW GAME BUTTON
    public void OnNewGameClicked()
    {
        Debug.Log("[SplashUI] Starting a TRUE NEW GAME…");

        // Clear all previous PlayerPrefs state
        PlayerPrefs.DeleteKey("LastScenario");
        PlayerPrefs.DeleteKey("LastCards");
        PlayerPrefs.DeleteKey("LastResponse");
        PlayerPrefs.DeleteKey("RemainingScenarios"); // <-- NEW SEQUENCER RESET

        PlayerPrefs.SetInt("SelectedSaveSlot", -1);
        PlayerPrefs.SetInt("FromGrading", 0);
        PlayerPrefs.Save();

        // Delete temp save if it exists
        SaveManager.Delete(-1);

        // Group 2: the menu flies apart and the synopsis comes up, no loading screen.
        if (MenuNewGameTransition.Play(gameObject.scene)) return;

        // Load loading screen, GameManager will generate new scenario & cards
        SceneTransition.Load("LoadingScene");
    }

    // CONTINUE
    public void OnContinueClicked()
    {
        SceneTransition.Load("SaveSelectScene");
    }

    // VIEW RESPONSES
    public void OnViewResponsesClicked()
    {
        SaveSelectUI.isReviewMode = true;
        SceneTransition.Load("SaveSelectScene");
    }

    // RULES
    public void OnRulesClicked()
    {
        SceneTransition.Load("RulesScene");
    }
}
