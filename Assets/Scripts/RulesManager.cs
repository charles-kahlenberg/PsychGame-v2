using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RulesManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject brainyImage;
    public GameObject speechBubble;
    public TMP_Text dialogueText;
    public Button continueButton;
    public Button returnMenuButton;          // Always visible now
    public Button showExampleButton;
    public ScrollRect exampleScrollView;      // NEW: assign in Inspector
    public TMP_Text exampleScrollText;        // TMP Text inside ScrollView Content

    private bool skipTyping = false;

    private List<string> rulesSections = new List<string>
    {
        "This game will have you use psychology terms in everyday situations. On each turn, the game will give you a scenario and 5 psychology terms to use.",
        "Not all the terms may apply for the given scenario! Do your best to write a description using as many of the given terms as you can, relating them to the scenario.",
        "The more descriptive you can be the better! If you need help, you can click on Brainy to give you a gentle nudge!",
        "Also, you can click on the 'Refreshes Left' button to get 5 different words to use in this scenario, if you did not like the first 5."
    };

    private string exampleText =
        "Scenario: Thomas has been feeling increasingly stressed and anxious lately due to work deadlines and personal responsibilities. " +
        "He decides to go for a run to help alleviate his stress and improve his mood. As he starts running, his heart rate increases, and he begins to feel a sense of exhilaration and euphoria. " +
        "Thomas wonders how exercise affects his brain and why he feels better after a run.\n\n" +
        "Psychology Terms: NEURONS, CLASSICALLY CONDITIONED, REM SLEEP, DEPTH PERCEPTION, ATTACHMENT\n\n" +
        "Your Description: When you run, your NEURONS start to output endorphins. Neurons are the way the brain communicates quickly to itself and the body. " +
        "Also, assuming you’ve ran before, you have probably made a CLASSICALLY CONDITIONED connection between running and how good you feel. " +
        "So, even as you begin to lace up your sneakers, you start to feel good. Exercise helps you sleep better, so you will get more REM SLEEP, which helps your body.";

    private int currentRuleIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && isTyping)
        {
            skipTyping = true;
        }
    }

    void Start()
    {
        if (TestGroups.IsEnabled(Feature.JarBrainy))
            JarBrainy.Apply(brainyImage);
        brainyImage.SetActive(false);
        speechBubble.SetActive(false);
        continueButton.gameObject.SetActive(false);
        returnMenuButton.gameObject.SetActive(true);    // Always visible
        showExampleButton.gameObject.SetActive(false);
        exampleScrollView.gameObject.SetActive(false);  // Hidden at first

        StartCoroutine(StartIntroSequence());
    }

    // Brainy's greeting used to come from OpenAI, called straight from the game
    // with a key built into it. That key was public, and the call held the
    // bubble empty for up to 10 seconds, so Brainy now always says this.
    private const string Greeting = "Hi! I’m Brainy. I’ll guide you in this psychology game!";

    IEnumerator StartIntroSequence()
    {
        yield return new WaitForSeconds(0.6f);
        brainyImage.SetActive(true);

        yield return new WaitForSeconds(0.4f);
        speechBubble.SetActive(true);

        yield return StartCoroutine(TypeText(Greeting, () =>
        {
            StartCoroutine(ShowContinueButtonAfterDelay());
        }));
    }

    IEnumerator ShowContinueButtonAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        continueButton.gameObject.SetActive(true);
    }

    public void OnContinueClicked()
    {
        continueButton.gameObject.SetActive(false);

        if (currentRuleIndex < rulesSections.Count)
        {
            // Make the rules font slightly bigger than normal dialogue
            // (Group 2's panel already sets a readable size).
            if (!TextBoxTheme.Enabled)
                dialogueText.fontSize = 24; // Adjust as needed (e.g., 32-40)

            typingCoroutine = StartCoroutine(TypeText(rulesSections[currentRuleIndex], () =>
            {
                currentRuleIndex++;

                if (currentRuleIndex < rulesSections.Count)
                {
                    continueButton.gameObject.SetActive(true);
                }
                else
                {
                    showExampleButton.gameObject.SetActive(true);
                }
            }));
        }
    }


    public void OnShowExampleClicked()
    {
        showExampleButton.gameObject.SetActive(false);

        // Group 2: Brainy's panel holds the example itself and scrolls it.
        if (TextBoxTheme.Enabled)
        {
            dialogueText.text = exampleText;
            return;
        }

        // Clear the bubble text
        dialogueText.text = "";

        exampleScrollView.gameObject.SetActive(true);
        exampleScrollText.text = exampleText;

        Canvas.ForceUpdateCanvases();
        exampleScrollView.verticalNormalizedPosition = 1f;
    }


    public void OnReturnMenuClicked()
    {
        SceneTransition.Load("SplashScene");
    }

    IEnumerator TypeText(string text, System.Action onComplete = null)
    {
        isTyping = true;
        skipTyping = false;

        // Group 2: laid out up front and uncovered, so words don't jump lines
        // and Brainy's panel is its final size from the first letter.
        if (TextBoxTheme.Enabled)
        {
            yield return TextPanel.Reveal(dialogueText, text, 0.03f, () => skipTyping);
            isTyping = false;
            skipTyping = false;
            onComplete?.Invoke();
            yield break;
        }

        dialogueText.text = "";
        dialogueText.ForceMeshUpdate();

        foreach (char c in text)
        {
            dialogueText.text += c;

            if (skipTyping)
            {
                dialogueText.text = text;
                break;
            }

            yield return new WaitForSeconds(0.03f);
        }

        isTyping = false;
        skipTyping = false;
        onComplete?.Invoke();
    }

}
