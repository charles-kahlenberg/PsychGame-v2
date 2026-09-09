using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashUI : MonoBehaviour
{
    public Button newGameButton;
    public Button continueButton;
    public Button viewResponsesButton;
    public Button studyModeButton;

    void Awake()
    {
        // Ensure SplashUI only exists in the SplashScene
        if (SceneManager.GetActiveScene().name != "SplashScene")
        {
            Destroy(gameObject);
            return;
        }
    }

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
        if (SceneManager.GetActiveScene().name != "SplashScene")
        {
            Debug.LogWarning("[SplashUI] NewGameClicked fired outside SplashScene — blocked.");
            return;
        }

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

        // Load loading screen, GameManager will generate new scenario & cards
        SceneManager.LoadScene("LoadingScene");
    }

    // CONTINUE
    public void OnContinueClicked()
    {
        SceneManager.LoadScene("SaveSelectScene");
    }

    // VIEW RESPONSES
    public void OnViewResponsesClicked()
    {
        SaveSelectUI.isReviewMode = true;
        SceneManager.LoadScene("SaveSelectScene");
    }

    // RULES
    public void OnRulesClicked()
    {
        SceneManager.LoadScene("RulesScene");
    }

    // STUDY MODE (future use)
    public void OnStudyModeClicked()
    {
        Debug.Log("Study Mode not implemented yet.");
    }
}
