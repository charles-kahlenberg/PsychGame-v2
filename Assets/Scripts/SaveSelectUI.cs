using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SaveSelectUI : MonoBehaviour
{
    public static bool isReviewMode = false;  // Keeps track if we're reviewing responses

    public Button[] slotButtons;
    public TextMeshProUGUI[] slotLabels;

    void Start()
    {
        RefreshSlots();
    }

    public void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool hasSave = SaveManager.HasSave(i);
            string title = SaveManager.GetTitle(i);

            slotButtons[i].interactable = hasSave;
            slotLabels[i].text = hasSave ? title : "Empty";

            Debug.Log($"[SaveSelectUI] Slot {i}: {(hasSave ? title : "Empty")}");
        }
    }

    public void SelectSlot(int index)
    {
        SaveManager.SetTempSaveSlot(index);
        PlayerPrefs.SetInt("SelectedSaveSlot", index);
        PlayerPrefs.Save();

        if (isReviewMode)
        {
            isReviewMode = false; // Reset for next use
            SceneManager.LoadScene("ReviewScene");
        }
        else
        {
            // Load IntroductionScene instead of LoadingScene
            if (SaveManager.HasSave(index))
            {
                SaveData data = SaveManager.Load(index);

                // Store the scenario and cards for the intro AI
                PlayerPrefs.SetString("LastScenario", data.scenario);
                PlayerPrefs.SetString("LastCards", string.Join("|", data.cards));
                PlayerPrefs.Save();
            }

            SceneManager.LoadScene("IntroductionScene");
        }
    }
}
