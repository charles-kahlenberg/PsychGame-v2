using UnityEngine;

public class SceneNavigation : MonoBehaviour
{
    public void GoToSplash()
    {
        SceneTransition.Load("SplashScene");
    }
}
