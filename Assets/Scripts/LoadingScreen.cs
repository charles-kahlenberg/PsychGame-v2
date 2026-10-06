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

        // New games, continues and the next round all show the intro first.
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(SynopsisTransition.SynopsisScene());

        while (!asyncLoad.isDone)
        {
            progressBar.value = Mathf.Clamp01(1f - (asyncLoad.progress < 0.9f ? 0.1f : 0f));
            yield return null;
        }
    }
}
