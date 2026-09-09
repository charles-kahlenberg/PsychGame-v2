using UnityEngine;
using UnityEngine.UI;

public class SplashController : MonoBehaviour
{
    public Button continueButton;

    void Start()
    {
        // Check PlayerPrefs for a saved key
        if (PlayerPrefs.GetInt("HasSave", 0) == 1)
        {
            Debug.Log("Save data found.");
            continueButton.interactable = true;
        }
        else
        {
            Debug.Log("No save data.");
            continueButton.interactable = false;
        }
    }
}
