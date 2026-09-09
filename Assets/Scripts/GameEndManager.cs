using UnityEngine;
using UnityEngine.SceneManagement;

public class GameEndManager : MonoBehaviour
{
    public void GoToMenu()
    {
        SceneManager.LoadScene("SplashScene");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene("GameScene");
    }
}
