using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public Slider progressBar;
    private float waitTime;

    void Start()
    {
        waitTime = Random.Range(3f, 6f);
        StartCoroutine(LoadGameAfterDelay());
    }

    IEnumerator LoadGameAfterDelay()
    {
        float elapsed = 0f;

        while (elapsed < waitTime)
        {
            elapsed += Time.deltaTime;
            progressBar.value = Mathf.Clamp01(elapsed / waitTime);
            yield return null;
        }

        // Decide which scene to load after loading
        string nextScene;

        // If we're coming from Grading OR starting a new game/continue, show intro first
        if (PlayerPrefs.GetInt("FromGrading", 0) == 1 ||
            PlayerPrefs.GetInt("SelectedSaveSlot", -1) != -1)
        {
            nextScene = "IntroductionScene";
        }
        else
        {
            nextScene = "IntroductionScene"; // New games also go to intro first
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextScene);

        while (!asyncLoad.isDone)
        {
            progressBar.value = Mathf.Clamp01(1f - (asyncLoad.progress < 0.9f ? 0.1f : 0f));
            yield return null;
        }
    }
}
