using UnityEngine;
using UnityEngine.SceneManagement;

public class GameEndManager : MonoBehaviour
{
    public void GoToMenu()
    {
        SceneTransition.Load("SplashScene");
    }

    public void RestartGame()
    {
        SceneTransition.Load("GameScene");
    }
}
