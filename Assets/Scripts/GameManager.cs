using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Save Prompt")]
    public GameObject savePromptPanel;
    public TMP_InputField saveNameInput;
    public int currentSaveSlot = -1;

    [Header("Game State")]
    public TextMeshProUGUI scenarioText;
    public TextMeshProUGUI[] cardTexts;
    public TMP_InputField responseInput;
    public List<string> currentCards = new List<string>();
    public string currentScenario;

    [Header("Tracking")]
    public List<string> usedVocab = new List<string>(); // PERMANENTLY burned
    public List<ScenarioResponse> responseHistory = new List<ScenarioResponse>();

    private List<string> allVocab = new List<string>();
    private Dictionary<string, string> termToDefinition = new Dictionary<string, string>();

    [Header("Card Refresh")]
    public Button refreshButton;
    public TextMeshProUGUI refreshCounterText;
    private int refreshUsesRemaining = 2;

    private bool isTyping = false;
    private bool skipTyping = false;

    private System.DateTime cardsShownAt;

    void Awake()
    {
        if (TestGroups.IsEnabled(Feature.RaisedHand))
            RaiseHand();
    }

    void Update()
    {
        if (isTyping && Input.GetMouseButtonDown(0))
            skipTyping = true;
    }

    void Start()
    {
        LoadVocab();

        currentSaveSlot = PlayerPrefs.GetInt("SelectedSaveSlot", -1);
        bool comingFromGrading = PlayerPrefs.GetInt("FromGrading", 0) == 1;

        if (comingFromGrading)
        {
            currentScenario = PlayerPrefs.GetString("LastScenario", "");

            string cardsString = PlayerPrefs.GetString("LastCards", "");
            currentCards = string.IsNullOrEmpty(cardsString)
                ? new List<string>()
                : new List<string>(cardsString.Split('|'));

            if (currentCards.Count == 0)
                GenerateNewCards();

            PlayerPrefs.SetInt("FromGrading", 0);
            PlayerPrefs.Save();
        }
        else
        {
            if (currentSaveSlot == -1)
            {
                // TRUE new game
                usedVocab.Clear();
                responseHistory.Clear();

                currentScenario = ScenarioSequencer.GetNextScenario();
                GenerateNewCards();
            }
            else if (SaveManager.HasSave(currentSaveSlot))
            {
                SaveData data = SaveManager.Load(currentSaveSlot);

                currentScenario = data.scenario;
                currentCards = data.cards ?? new List<string>();
                usedVocab = data.usedVocab ?? new List<string>();
                responseHistory = data.responses ?? new List<ScenarioResponse>();

                if (currentCards.Count == 0)
                    GenerateNewCards();
            }
            else
            {
                currentScenario = ScenarioSequencer.GetNextScenario();
                GenerateNewCards();
            }
        }

        StartCoroutine(TypeText(scenarioText, currentScenario, 0.03f));

        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].text = i < currentCards.Count ? currentCards[i] : "[Empty]";

        SetCardBacks();
        DealCardArt();

        refreshUsesRemaining = 2;
        UpdateRefreshUI();

        ClickLogger.SetActiveScenario(currentScenario);
        ClickLogger.LogCardEvent(currentScenario, "initial", currentCards, -1);
        cardsShownAt = System.DateTime.UtcNow;
    }

    // -------------------- VOCAB --------------------

    void LoadVocab()
    {
        TextAsset vocabAsset = Resources.Load<TextAsset>("cleaned_terms");
        if (vocabAsset == null) return;

        allVocab.Clear();
        termToDefinition.Clear();

        foreach (string line in vocabAsset.text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split(new[] { " - " }, System.StringSplitOptions.None);
            if (parts.Length < 2) continue;

            string term = System.Text.RegularExpressions.Regex
                .Replace(parts[0].Trim(), @"^\d+\s*", "");

            allVocab.Add(term);
            termToDefinition[term] = parts[1].Trim();
        }
    }

    // -------------------- CARD GENERATION --------------------
    // IMPORTANT: DOES NOT burn cards
    void GenerateNewCards()
    {
        currentCards.Clear();

        List<string> pool = new List<string>();
        foreach (string v in allVocab)
            if (!usedVocab.Contains(v))
                pool.Add(v);

        while (currentCards.Count < 5 && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            currentCards.Add(pool[index]);
            pool.RemoveAt(index); // prevent duplicates in same hand
        }

        PlayerPrefs.SetString("LastCards", string.Join("|", currentCards));
        PlayerPrefs.Save();
    }

    void SetCardBacks()
    {
        for (int i = 0; i < currentCards.Count; i++)
        {
            GameObject cardObj = GameObject.Find($"Card{i + 1}");
            if (!cardObj) continue;

            TMP_Text back = cardObj.transform
                .Find($"BackFace/BackText{i + 1}")
                ?.GetComponent<TMP_Text>();

            if (back != null && termToDefinition.ContainsKey(currentCards[i]))
                back.text = termToDefinition[currentCards[i]];
        }
    }

    // Group 2 (Feature.NewCardArt): each card wears the art for its term's
    // area of psychology. A term in several areas gets one of them at random,
    // kept until the next hand is shown.
    void DealCardArt()
    {
        if (!TestGroups.IsEnabled(Feature.NewCardArt)) return;

        CardArt art = CardArt.Load();
        if (art == null)
        {
            Debug.LogWarning($"[GameManager] No CardArt asset at Resources/{CardArt.ResourcePath}.");
            return;
        }

        for (int i = 0; i < currentCards.Count; i++)
        {
            GameObject cardObj = GameObject.Find($"Card{i + 1}");
            CardBehavior card = cardObj ? cardObj.GetComponent<CardBehavior>() : null;
            if (card == null) continue;

            PsychArea? area = TermAreas.PickArea(currentCards[i]);
            if (area == null) Debug.LogWarning($"[GameManager] No area listed for \"{currentCards[i]}\" in term_areas.txt.");
            card.ShowArt(art.For(area));
        }
    }

    // -------------------- GROUP 2 RAISED HAND --------------------
    // Feature.RaisedHand: the scene's hand hangs off the bottom of the screen
    // (the outer cards' corners are cut off). Shrink the hand a little,
    // flatten its fan, and lift it until its lowest corner clears the bottom
    // edge; then make room above it by nudging the response box up and
    // trimming its bottom. Works from the cards' real corners in canvas
    // units, and the canvas scales to match screen height, so it holds at
    // any window shape. Runs in Awake, before the cards and Brainy record
    // their resting spots in Start.

    private const float HandScale = 0.85f;        // card size and spread
    private const float HandFanFlatten = 0.8f;    // outer cards' tilt and droop
    private const float HandBottomMargin = 12f;   // room for the idle bob and hover pop
    private const float HandToBoxGap = 4f;        // between the hand's top and the response box
    private const float BoxMaxRaise = 4f;         // any higher and the box meets the speech bubble's tail

    void RaiseHand()
    {
        var cards = new List<RectTransform>();
        for (int i = 1; i <= 5; i++)
        {
            GameObject cardObj = GameObject.Find($"Card{i}");
            if (cardObj) cards.Add((RectTransform)cardObj.transform);
        }
        if (cards.Count == 0) return; // not the game screen

        // Shrink and flatten the fan around its middle card.
        Vector2 middle = cards[cards.Count / 2].anchoredPosition;
        foreach (RectTransform card in cards)
        {
            Vector2 offset = card.anchoredPosition - middle;
            card.anchoredPosition = middle + new Vector2(offset.x * HandScale, offset.y * HandFanFlatten * HandScale);
            card.localRotation = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(0f, card.localEulerAngles.z) * HandFanFlatten);
            card.localScale *= HandScale;
        }

        // The cards are anchored at the canvas's center, and the scaler
        // matches height, so the bottom edge is always half the reference
        // height below them.
        var scaler = cards[0].GetComponentInParent<CanvasScaler>();
        float canvasHeight = scaler ? scaler.referenceResolution.y : 450f;
        float canvasBottom = -canvasHeight / 2f;

        float lowest = float.MaxValue;
        foreach (RectTransform card in cards) lowest = Mathf.Min(lowest, CardExtentY(card, bottom: true));
        float lift = Mathf.Max(0f, canvasBottom + HandBottomMargin - lowest);
        foreach (RectTransform card in cards) card.anchoredPosition += new Vector2(0f, lift);

        float handTop = float.MinValue;
        foreach (RectTransform card in cards) handTop = Mathf.Max(handTop, CardExtentY(card, bottom: false));

        // Make room above the hand: raise the response box a little, then
        // trim its bottom edge for the rest, keeping its top where it ends up.
        RectTransform box = responseInput ? responseInput.GetComponent<RectTransform>() : null;
        if (box == null) return;

        float boxBottom = (box.anchorMin.y - 0.5f) * canvasHeight + box.anchoredPosition.y - box.rect.height * box.pivot.y;
        float needed = handTop + HandToBoxGap - boxBottom;
        if (needed <= 0f) return;

        float raise = Mathf.Min(needed, BoxMaxRaise);
        float trim = needed - raise;
        box.sizeDelta -= new Vector2(0f, trim);
        box.anchoredPosition += new Vector2(0f, raise + trim * box.pivot.y);

        // The submit button sits at the box's bottom edge, so it moves with it.
        GameObject submit = GameObject.Find("SubmitButton");
        if (submit) ((RectTransform)submit.transform).anchoredPosition += new Vector2(0f, needed);
    }

    // The card's lowest (or highest) corner, in its parent's space.
    static float CardExtentY(RectTransform card, bool bottom)
    {
        Rect r = card.rect;
        float extreme = bottom ? float.MaxValue : float.MinValue;
        foreach (var corner in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax) })
        {
            Vector3 p = card.localRotation * Vector3.Scale(corner, card.localScale);
            float y = card.anchoredPosition.y + p.y;
            extreme = bottom ? Mathf.Min(extreme, y) : Mathf.Max(extreme, y);
        }
        return extreme;
    }

    // -------------------- REFRESH --------------------

    public void RefreshCards()
    {
        if (refreshUsesRemaining <= 0) return;

        long elapsedMs = (long)(System.DateTime.UtcNow - cardsShownAt).TotalMilliseconds;

        GenerateNewCards();
        refreshUsesRemaining--;
        UpdateRefreshUI();

        if (TestGroups.IsEnabled(Feature.CardTweening))
            StartCoroutine(AnimateCardRefresh());
        else
            ShowCurrentCards();

        ClickLogger.LogCardEvent(currentScenario, "refresh", currentCards, elapsedMs);
        cardsShownAt = System.DateTime.UtcNow;
    }

    void ShowCurrentCards()
    {
        for (int i = 0; i < cardTexts.Length; i++)
            cardTexts[i].text = i < currentCards.Count ? currentCards[i] : "[Empty]";

        SetCardBacks();
        DealCardArt();

        // Group 2: new terms are dealt face-down; the player flips each one to see it.
        if (TestGroups.IsEnabled(Feature.FaceDownCards))
        {
            foreach (var card in FindObjectsByType<CardBehavior>(FindObjectsSortMode.None))
                card.TurnFaceDown();
        }
    }

    // Group 2 (Feature.CardTweening): the old hand drops away, the new words
    // are swapped in while the cards are off screen, then the hand is dealt
    // back in. Refresh is locked until the new hand has landed.
    IEnumerator AnimateCardRefresh()
    {
        if (refreshButton) refreshButton.interactable = false;

        var cards = FindObjectsByType<CardBehavior>(FindObjectsSortMode.None);
        System.Array.Sort(cards, (a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < cards.Length; i++)
            cards[i].SweepOut(i * CardBehavior.DealStagger);

        yield return new WaitForSeconds(CardBehavior.SweepOutTime(cards.Length));

        ShowCurrentCards();

        for (int i = 0; i < cards.Length; i++)
            cards[i].DealIn(i * CardBehavior.DealStagger);

        yield return new WaitForSeconds(CardBehavior.DealInTime(cards.Length));
        UpdateRefreshUI();
    }

    void UpdateRefreshUI()
    {
        if (refreshButton)
            refreshButton.interactable = refreshUsesRemaining > 0;

        if (refreshCounterText)
            refreshCounterText.text = $"Refreshes Left: {refreshUsesRemaining}";
    }

    // -------------------- SUBMIT (BURN HAPPENS HERE) --------------------

    public void OnSubmitResponse(TMP_InputField field)
    {
        string userResponse = field.text.Trim();

        // Burn ONLY the cards currently visible
        foreach (string card in currentCards)
        {
            if (!usedVocab.Contains(card))
                usedVocab.Add(card);
        }

        responseHistory.Add(new ScenarioResponse
        {
            scenario = currentScenario,
            response = userResponse,
            aiFeedback = ""
        });

        ClickLogger.LogUserResponse(currentScenario, userResponse);

        // THIS WAS MISSING
        PlayerPrefs.SetString("LastResponse", userResponse);

        SaveProgress();

        PlayerPrefs.SetString("LastScenario", currentScenario);
        PlayerPrefs.SetString("LastCards", string.Join("|", currentCards));
        PlayerPrefs.SetInt("FromGrading", 1);
        PlayerPrefs.Save();

        field.text = "";
        SceneTransition.Load("GradingScene");
    }


    // -------------------- TYPING --------------------

    IEnumerator TypeText(TextMeshProUGUI textObj, string fullText, float delay)
    {
        isTyping = true;
        skipTyping = false;
        textObj.text = "";

        foreach (char c in fullText)
        {
            if (skipTyping)
            {
                textObj.text = fullText;
                break;
            }

            textObj.text += c;
            yield return new WaitForSeconds(delay);
        }

        isTyping = false;
    }

    // -------------------- SAVE / LOAD --------------------

    void SaveProgress()
    {
        SaveManager.Save(currentSaveSlot, new SaveData
        {
            title = SaveManager.GetTitle(currentSaveSlot),
            scenario = currentScenario,
            cards = new List<string>(currentCards),
            usedVocab = new List<string>(usedVocab),
            responses = new List<ScenarioResponse>(responseHistory)
        });
    }

    public void StartNewGame()
    {
        PlayerPrefs.DeleteKey("LastScenario");
        PlayerPrefs.DeleteKey("LastCards");
        PlayerPrefs.DeleteKey("LastResponse");
        PlayerPrefs.DeleteKey("RemainingScenarios");

        PlayerPrefs.SetInt("SelectedSaveSlot", -1);
        PlayerPrefs.SetInt("FromGrading", 0);
        PlayerPrefs.Save();

        usedVocab.Clear();
        responseHistory.Clear();

        SceneTransition.Load("LoadingScene");
    }

    public void SetNewGameSlot(int idx)
    {
        SaveManager.SetTempSaveSlot(idx);
        PlayerPrefs.SetInt("SelectedSaveSlot", idx);
        PlayerPrefs.DeleteKey("RemainingScenarios");
        PlayerPrefs.Save();

        SceneTransition.Load("LoadingScene");
    }

    public void ContinueGame() => SceneTransition.Load("SaveSelectScene");

    public void PromptSave() => savePromptPanel.SetActive(true);

    public void CancelAndExit()
    {
        savePromptPanel.SetActive(false);
        SceneTransition.Load("SplashScene");
    }

    public void ConfirmSave()
    {
        // Ensure we have a real slot
        if (currentSaveSlot == -1)
        {
            currentSaveSlot = SaveManager.GetFirstAvailableSlot();
            PlayerPrefs.SetInt("SelectedSaveSlot", currentSaveSlot);
        }

        string name = string.IsNullOrWhiteSpace(saveNameInput.text)
            ? $"Save {currentSaveSlot + 1}"
            : saveNameInput.text;

        SaveData data = new SaveData
        {
            title = name,
            scenario = currentScenario,
            cards = new List<string>(currentCards),
            usedVocab = new List<string>(usedVocab),
            responses = new List<ScenarioResponse>(responseHistory)
        };

        SaveManager.Save(currentSaveSlot, data);

        PlayerPrefs.Save();

        savePromptPanel.SetActive(false);
        SceneTransition.Load("SplashScene");
    }

}
