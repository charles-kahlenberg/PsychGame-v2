using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntroductionManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject npcImage;
    public GameObject speechBubble;
    public TMP_Text dialogueText;
    public Button continueButton;

    [Header("Cloudflare Worker")]
    [Tooltip("Your Worker URL, e.g. https://psych-ai.charliekahlenberg.workers.dev/intro")]
    public string introductionWorkerUrl = "https://psych-ai.charliekahlenberg.workers.dev/intro";

    private string currentScenario;
    private bool skipTyping = false;
    private string introText; // null until the worker's reply is in
    private GameObject scenarioImage;
    private bool overviewShowing; // Feature.GameOverview: Continue moves on to the synopsis

    private const string GameOverview =
        "Here’s how the game works: each round, I’ll show you an everyday scenario and deal you 5 psychology terms. " +
        "Write a response that explains the scenario using as many of the terms as you can. " +
        "If you get stuck, click on me for a nudge. Now, let’s look at your first scenario.";

    // Before TextBoxTheme fits the speech bubble around the NPC (on scene load).
    void Awake()
    {
        if (TestGroups.IsEnabled(Feature.JarAvatar))
            JarBrainy.ApplyAvatar(npcImage);
    }

    void Start()
    {
        currentScenario = ScenarioSequencer.Current() ?? "";

        npcImage.SetActive(false);
        speechBubble.SetActive(false);
        continueButton.gameObject.SetActive(false);

        if (TestGroups.IsEnabled(Feature.ScenarioImage))
            AddScenarioImage();

        // Asked for now, while the NPC walks in, rather than once they've
        // arrived: waiting until then left an empty bubble for seconds.
        StartCoroutine(RequestWorkerIntro());
        StartCoroutine(StartIntroSequence());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            skipTyping = true;
    }

    IEnumerator StartIntroSequence()
    {
        // Group 2: the last grading screen, or the menu after New Game, may
        // still be on its way out over this one.
        while (GradingTransition.Covering || MenuNewGameTransition.Covering) yield return null;

        yield return new WaitForSeconds(0.3f);

        npcImage.SetActive(true);
        Vector3 startPos = npcImage.transform.localPosition;
        npcImage.transform.localPosition = new Vector3(800f, startPos.y, startPos.z);
        LeanTween.moveLocalX(npcImage, startPos.x, 0.8f).setEaseOutBack();

        yield return new WaitForSeconds(0.6f);

        speechBubble.SetActive(true);
        if (GradingTransition.Enabled) GradingTransition.SlideIn(speechBubble.transform, Vector2.up, 0f);

        // Feature.GameOverview: the first scenario opens with how the game
        // works, then the bubble fades and comes back with the synopsis.
        if (TestGroups.IsEnabled(Feature.GameOverview) && ScenarioNumber() == 1)
        {
            if (scenarioImage != null) scenarioImage.SetActive(false);
            skipTyping = false;
            yield return TextPanel.Type(dialogueText, GameOverview, 0.03f, () => skipTyping);
            overviewShowing = true;
            continueButton.gameObject.SetActive(true);
            while (overviewShowing) yield return null;
            continueButton.gameObject.SetActive(false);
            yield return SwapBubble(() =>
            {
                dialogueText.text = "";
                if (scenarioImage != null) scenarioImage.SetActive(true);
            });
        }

        // Still waiting on the worker: "..." so the NPC is clearly about to speak.
        for (float t = 0f; introText == null; t += Time.deltaTime)
        {
            dialogueText.text = new string('.', 1 + (int)(t / 0.35f) % 3);
            yield return null;
        }

        skipTyping = false;
        yield return TextPanel.Type(dialogueText, introText, 0.03f, () => skipTyping);
        continueButton.gameObject.SetActive(true);
    }

    // The bubble fades up and away, midway() changes what's in it, and it
    // settles back in, as Continue's own fade out does.
    IEnumerator SwapBubble(Action midway)
    {
        if (!speechBubble.TryGetComponent(out CanvasGroup group)) group = speechBubble.AddComponent<CanvasGroup>();
        Transform bubble = speechBubble.transform;
        Vector3 rest = bubble.localPosition;
        const float time = 0.3f;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = 1f - k;
            bubble.localPosition = rest + Vector3.up * 16f * k;
            yield return null;
        }
        midway();
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = k;
            bubble.localPosition = rest + Vector3.up * 16f * (1f - k);
            yield return null;
        }
        group.alpha = 1f;
        bubble.localPosition = rest;
    }

    // 1 = the first in Scenarios.txt, 0 if it isn't there.
    private int ScenarioNumber() => ScenarioSequencer.LoadScenariosInOrder().IndexOf(currentScenario.Trim()) + 1;

    IEnumerator RequestWorkerIntro()
    {
        // Your worker accepts cards, but doesn't require them.
        // Group 2: the response screen has already dealt underneath, so ask
        // with the cards from before, as the intro always has.
        WorkerIntroRequest payload = new WorkerIntroRequest
        {
            scenario = currentScenario ?? "",
            cards = Worker.Cards(SynopsisTransition.CardsBeforeDeal ?? PlayerPrefs.GetString("LastCards", "")).ToArray()
        };

        string resp = null;
        yield return Worker.Post(introductionWorkerUrl, JsonUtility.ToJson(payload), r => resp = r);

        string intro = Worker.Read(resp, r => r.intro);
        if (resp != null && intro == null)
            Debug.LogWarning("[IntroductionManager] Worker response did not contain 'intro'. Raw: " + resp);

        // Fallback if something fails
        string speaker = TestGroups.IsEnabled(Feature.JarAvatar) ? "Brainy" : "Alex";
        introText = intro != null ? intro.Trim() : $"Hi, I’m {speaker}! I really need your help with something important.";
    }

    // Feature.ScenarioImage: the scenario's picture in a paper frame under the
    // speech bubble, from Resources/ScenarioImages/<scenario number> (1 = the
    // first in Scenarios.txt), else Placeholder. A child of the bubble, so it
    // follows the bubble's height and fades out with it on Continue.
    private void AddScenarioImage()
    {
        int number = ScenarioNumber();
        string folder = "ScenarioImages/" + number + "/"; // layered art, animated by PhoneScenarioArt, ChocolateScenarioArt or SurgeonScenarioArt
        Sprite sprite = Resources.Load<Sprite>(folder + "Background") ?? Resources.Load<Sprite>("ScenarioImages/" + number) ?? Resources.Load<Sprite>("ScenarioImages/Placeholder");
        if (sprite == null) return;

        const float gap = 16f, maxHeight = 320f, mat = 3f;

        // Under the bubble's left 85%, clear of its tail; the frame fits
        // inside at the picture's aspect.
        var area = new GameObject("ScenarioImage", typeof(RectTransform)).GetComponent<RectTransform>();
        scenarioImage = area.gameObject;
        area.SetParent(speechBubble.transform, false);
        area.anchorMin = Vector2.zero;
        area.anchorMax = new Vector2(0.85f, 0f);
        area.pivot = new Vector2(0.5f, 1f);
        area.anchoredPosition = new Vector2(0f, -gap);
        area.sizeDelta = new Vector2(0f, maxHeight);

        var frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(area, false);
        var fit = frame.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = (sprite.rect.width / sprite.rect.height * (maxHeight - 2f * mat) + 2f * mat) / maxHeight;
        TextPanel.AddPaper(frame);
        frame.GetComponent<Image>().raycastTarget = false; // don't drag-scroll the bubble
        // A quarter of the panels' corner radius (2.5 units, under the mat),
        // so the corners keep the same margin around the picture as the sides.
        frame.Find("Paper").GetComponent<Image>().pixelsPerUnitMultiplier = PanelSprites.Density * 4f;
        frame.Find("Shadow").GetComponent<Image>().pixelsPerUnitMultiplier = PanelSprites.Density * 4f;

        var picture = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        picture.SetParent(frame, false);
        picture.anchorMin = Vector2.zero;
        picture.anchorMax = Vector2.one;
        picture.offsetMin = new Vector2(mat, mat);
        picture.offsetMax = new Vector2(-mat, -mat);
        var image = picture.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        PhoneScenarioArt.Add(picture, folder);
        ChocolateScenarioArt.Add(picture, folder);
        SurgeonScenarioArt.Add(picture, folder);
    }

    public void OnContinueClicked()
    {
        if (overviewShowing)
        {
            overviewShowing = false;
            return;
        }


        // Group 2: the screen turns into the response screen instead of fading.
        if (SynopsisTransition.Enabled)
        {
            StartCoroutine(SynopsisTransition.Leave((RectTransform)npcImage.transform, speechBubble, continueButton.gameObject));
            return;
        }
        SceneTransition.Load("GameScene");
    }

    [Serializable]
    private class WorkerIntroRequest
    {
        public string scenario;
        public string[] cards;
    }
}
