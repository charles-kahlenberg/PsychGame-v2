using UnityEngine;
using UnityEngine.UI;

public class LoadingBlinkImage : MonoBehaviour
{
    public Image loadingImage;        // Assign your "Loading..." image here
    public float blinkSpeed = 1f;     // How fast it blinks
    public float minAlpha = 0.3f;     // Minimum fade (how transparent)
    public float maxAlpha = 1f;       // Maximum fade (fully visible)

    private Color originalColor;
    private float timer;

    void Start()
    {
        if (loadingImage == null)
            loadingImage = GetComponent<Image>();

        originalColor = loadingImage.color;
    }

    void Update()
    {
        timer += Time.deltaTime * blinkSpeed;

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(timer) + 1) / 2f);

        Color c = originalColor;
        c.a = alpha;
        loadingImage.color = c;
    }
}
