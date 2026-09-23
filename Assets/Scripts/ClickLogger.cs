using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Captures research telemetry during play and reports it to the research
// logging backend (Cloudflare Worker -> D1): clicks, AI-generated text,
// user-written responses, card deals/refreshes, and time spent per screen.
// Bootstraps itself on startup so it doesn't need to be placed in any scene.
public class ClickLogger : MonoBehaviour
{
    private const string WorkerUrl = "https://psych-log.charliekahlenberg.workers.dev";

    // Scenes whose time-on-screen we report, and what to call them in the data.
    private static readonly Dictionary<string, string> SceneToMenuName = new Dictionary<string, string>
    {
        { "SplashScene", "main_menu" },
        { "GameScene", "respond" },
        { "ReviewScene", "review" },
    };

    private static ClickLogger _instance;
    private string _sessionId;
    private string _username;

    // Every logged event has a foreign key on sessions, so nothing is sent
    // before /session/start finishes (it would be silently dropped by the FK
    // constraint) -- events just keep piling up in the pending lists below
    // until the session is ready and the next flush point arrives.
    private bool _sessionReady;

    // All logging events are cached here rather than sent as they happen,
    // and are only flushed to the backend once per menu load (see
    // FlushBatch), to keep network chatter to one request per screen instead
    // of one per click/response/card event.
    private readonly List<ClickLogPayload> _pendingClicks = new List<ClickLogPayload>();
    private readonly List<AiResponsePayload> _pendingAiResponses = new List<AiResponsePayload>();
    private readonly List<UserResponsePayload> _pendingUserResponses = new List<UserResponsePayload>();
    private readonly List<CardEventPayload> _pendingCardEvents = new List<CardEventPayload>();
    private readonly List<MenuDurationPayload> _pendingMenuDurations = new List<MenuDurationPayload>();

    private string _currentMenuName;
    private DateTime _menuEnteredAt;
    private string _activeScenario;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        var go = new GameObject("ClickLogger");
        _instance = go.AddComponent<ClickLogger>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        _sessionId = Guid.NewGuid().ToString();
        StartCoroutine(BootstrapUsernamePrompt());

        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(EnterInitialMenuNextFrame());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Awake() runs during the BeforeSceneLoad bootstrap callback, which is
    // early enough that GetActiveScene() isn't guaranteed to report the real
    // first scene yet. Waiting a frame guarantees it has settled.
    private IEnumerator EnterInitialMenuNextFrame()
    {
        yield return null;
        EnterMenu(SceneManager.GetActiveScene().name);
    }

    private void OnApplicationQuit()
    {
        ExitMenu();
        FlushBatch();
    }

    // WebGL does not reliably call OnApplicationQuit when the player just
    // closes the browser tab, but it does call this via the page visibility
    // API — so this is the flush point that actually fires in practice.
    // There's no further menu load coming after this, so flush immediately
    // rather than caching for a load that may never happen.
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ExitMenu();
            FlushBatch();
        }
        else
        {
            EnterMenu(SceneManager.GetActiveScene().name);
        }
    }

    private IEnumerator BootstrapUsernamePrompt()
    {
        // Wait for the current scene's own EventSystem to exist (we
        // bootstrap BeforeSceneLoad, so at frame 0 none has loaded yet) so
        // the prompt doesn't create a second, conflicting EventSystem.
        for (int i = 0; i < 10 && EventSystem.current == null; i++)
        {
            yield return null;
        }

        string savedUsername = PlayerPrefs.GetString("LastUsername", "");
        UsernamePromptUI.Show(savedUsername, username =>
        {
            _username = username;
            if (!string.IsNullOrEmpty(username))
            {
                PlayerPrefs.SetString("LastUsername", username);
                PlayerPrefs.Save();
            }
            StartCoroutine(StartSession());
        });
    }

    private IEnumerator StartSession()
    {
        yield return PostJson("/session/start", JsonUtility.ToJson(new SessionStartPayload { sessionId = _sessionId, username = _username }));

        _sessionReady = true;
        // Send anything that piled up (e.g. clicks during the username
        // prompt) instead of waiting for the next menu load.
        FlushBatch();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            LogClick(Input.mousePosition);
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            LogClick(Input.GetTouch(0).position);
        }
    }

    // -------------------- SCENE / MENU TIMING --------------------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ExitMenu();
        EnterMenu(scene.name);

        // Every menu load is the flush point: whatever was cached during the
        // menu that just ended (clicks, responses, card events, and the
        // duration row ExitMenu just added) goes out as a single request.
        FlushBatch();
    }

    private void EnterMenu(string sceneName)
    {
        if (SceneToMenuName.TryGetValue(sceneName, out string menuName))
        {
            _currentMenuName = menuName;
            _menuEnteredAt = DateTime.UtcNow;
        }
        else
        {
            _currentMenuName = null;
        }
    }

    private void ExitMenu()
    {
        if (_currentMenuName == null) return;

        long durationMs = (long)(DateTime.UtcNow - _menuEnteredAt).TotalMilliseconds;

        // "respond" is the only menu tied to a specific scenario. GameManager
        // hands it to us directly via SetActiveScenario as soon as it's known,
        // so this is correct regardless of how the player leaves GameScene
        // (Submit, Save & Exit, etc.) rather than only on a successful submit.
        string scenario = _currentMenuName == "respond" ? _activeScenario : null;

        _pendingMenuDurations.Add(new MenuDurationPayload
        {
            sessionId = _sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            menuName = _currentMenuName,
            durationMs = durationMs,
            scenario = scenario,
        });

        _currentMenuName = null;
    }

    // -------------------- PUBLIC LOGGING API --------------------

    // Tells the logger which scenario the "respond" screen is currently
    // showing, so a menu-duration row logged for it is correct no matter
    // how the player leaves (submit, save & exit, etc).
    public static void SetActiveScenario(string scenario)
    {
        if (_instance == null) return;
        _instance._activeScenario = scenario;
    }

    // score: the numeric grade Brainy gave (0-100), when kind is
    // "grading_feedback". Pass -1 (the default) when there's no score to
    // report, e.g. for hints/concept help, or when grading text didn't
    // parse to a number.
    public static void LogAiResponse(string kind, string scenario, string content, int score = -1)
    {
        if (_instance == null) return;

        _instance._pendingAiResponses.Add(new AiResponsePayload
        {
            sessionId = _instance._sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            kind = kind,
            scenario = scenario,
            content = content,
            score = score,
        });
    }

    public static void LogUserResponse(string scenario, string response)
    {
        if (_instance == null) return;

        _instance._pendingUserResponses.Add(new UserResponsePayload
        {
            sessionId = _instance._sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            scenario = scenario,
            response = response,
        });
    }

    // elapsedMs: how long the previous card set was up before this one
    // replaced it. Pass -1 for the initial deal (nothing to measure yet).
    public static void LogCardEvent(string scenario, string eventType, List<string> cards, long elapsedMs)
    {
        if (_instance == null) return;

        _instance._pendingCardEvents.Add(new CardEventPayload
        {
            sessionId = _instance._sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            scenario = scenario,
            eventType = eventType,
            cards = cards != null ? string.Join("|", cards) : "",
            elapsedMs = elapsedMs,
        });
    }

    // -------------------- CLICK LOGGING --------------------

    private void LogClick(Vector2 screenPosition)
    {
        _pendingClicks.Add(new ClickLogPayload
        {
            sessionId = _sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            objectName = ResolveClickedObjectName(screenPosition),
        });
    }

    // Walks up from the raycast hit to the nearest Button/Toggle/CardBehavior
    // so a click anywhere on a card or button resolves to its real editor
    // name (e.g. "Card1", "RefreshCardsButton") instead of a child label's.
    private string ResolveClickedObjectName(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return null;

        var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0) return null;

        for (Transform t = results[0].gameObject.transform; t != null; t = t.parent)
        {
            if (t.GetComponent<Button>() != null ||
                t.GetComponent<Toggle>() != null ||
                t.GetComponent<CardBehavior>() != null)
            {
                return t.name;
            }
        }

        return results[0].gameObject.name;
    }

    // -------------------- TRANSPORT --------------------

    // Sends everything cached so far as one request and clears the pending
    // lists. A no-op until the session is ready (nothing is dropped -- it
    // just keeps accumulating for the next call) and when there's nothing
    // new to report.
    private void FlushBatch()
    {
        if (!_sessionReady) return;

        if (_pendingClicks.Count == 0 && _pendingAiResponses.Count == 0 &&
            _pendingUserResponses.Count == 0 && _pendingCardEvents.Count == 0 &&
            _pendingMenuDurations.Count == 0)
        {
            return;
        }

        var batch = new LogBatchPayload
        {
            clicks = _pendingClicks.ToArray(),
            aiResponses = _pendingAiResponses.ToArray(),
            userResponses = _pendingUserResponses.ToArray(),
            cardEvents = _pendingCardEvents.ToArray(),
            menuDurations = _pendingMenuDurations.ToArray(),
        };

        StartCoroutine(PostJson("/log/batch", JsonUtility.ToJson(batch)));

        _pendingClicks.Clear();
        _pendingAiResponses.Clear();
        _pendingUserResponses.Clear();
        _pendingCardEvents.Clear();
        _pendingMenuDurations.Clear();
    }

    private IEnumerator PostJson(string path, string json)
    {
        using (var req = new UnityWebRequest(WorkerUrl + path, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ClickLogger] Failed to send log to {path}: {req.error}");
            }
        }
    }

    [Serializable]
    private class SessionStartPayload
    {
        public string sessionId;
        public string username;
    }

    [Serializable]
    private class ClickLogPayload
    {
        public string sessionId;
        public string timestamp;
        public string objectName;
    }

    [Serializable]
    private class AiResponsePayload
    {
        public string sessionId;
        public string timestamp;
        public string kind;
        public string scenario;
        public string content;
        public int score;
    }

    [Serializable]
    private class UserResponsePayload
    {
        public string sessionId;
        public string timestamp;
        public string scenario;
        public string response;
    }

    [Serializable]
    private class CardEventPayload
    {
        public string sessionId;
        public string timestamp;
        public string scenario;
        public string eventType;
        public string cards;
        public long elapsedMs;
    }

    [Serializable]
    private class MenuDurationPayload
    {
        public string sessionId;
        public string timestamp;
        public string menuName;
        public long durationMs;
        public string scenario;
    }

    [Serializable]
    private class LogBatchPayload
    {
        public ClickLogPayload[] clicks;
        public AiResponsePayload[] aiResponses;
        public UserResponsePayload[] userResponses;
        public CardEventPayload[] cardEvents;
        public MenuDurationPayload[] menuDurations;
    }
}
