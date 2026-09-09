using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
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


    [Header("AI Settings")]
    public string openAIKey = "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";

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
        "Also, assuming you致e ran before, you have probably made a CLASSICALLY CONDITIONED connection between running and how good you feel. " +
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
        brainyImage.SetActive(false);
        speechBubble.SetActive(false);
        continueButton.gameObject.SetActive(false);
        returnMenuButton.gameObject.SetActive(true);    // Always visible
        showExampleButton.gameObject.SetActive(false);
        exampleScrollView.gameObject.SetActive(false);  // Hidden at first

        StartCoroutine(StartIntroSequence());
    }

    IEnumerator StartIntroSequence()
    {
        yield return new WaitForSeconds(1.5f);
        brainyImage.SetActive(true);

        yield return new WaitForSeconds(1f);
        speechBubble.SetActive(true);

        yield return StartCoroutine(RequestAIIntro());
    }

    IEnumerator RequestAIIntro()
    {

        string prompt =
            "Introduce yourself as Brainy, the friendly psychology helper trapped in a jar. " +
            "Speak in first person, like 'Hi! I知 Brainy�'. Be cheerful and helpful, and say that you値l guide the player in this psychology game.";

        string finalText = "Hi! I知 Brainy. I値l guide you in this psychology game!";

        var requestData = new
        {
            model = "gpt-5",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_completion_tokens = 100
        };


        string jsonBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

        using (UnityWebRequest request = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + openAIKey);

            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                JObject result = JObject.Parse(jsonResponse);
                string content = result["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                if (!string.IsNullOrEmpty(content))
                    finalText = content;
            }
        }

        yield return StartCoroutine(TypeText(finalText, () =>
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

        // Clear the bubble text
        dialogueText.text = "";

        exampleScrollView.gameObject.SetActive(true);
        exampleScrollText.text = exampleText;

        Canvas.ForceUpdateCanvases();
        exampleScrollView.verticalNormalizedPosition = 1f;
    }


    public void OnReturnMenuClicked()
    {
        SceneManager.LoadScene("SplashScene");
    }

    IEnumerator TypeText(string text, System.Action onComplete = null)
    {
        isTyping = true;
        skipTyping = false;
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
