using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

// Captures a timestamp for every click during play and reports it to the
// research logging backend (Cloudflare Worker -> D1). Bootstraps itself on
// startup so it doesn't need to be placed in any scene.
public class ClickLogger : MonoBehaviour
{
    private const string WorkerUrl = "https://psych-log.charliekahlenberg.workers.dev";

    private static ClickLogger _instance;
    private string _sessionId;

    // click_events has a foreign key on sessions, so any click that fires
    // before /session/start finishes has to wait rather than be sent (and
    // silently dropped by the FK constraint).
    private bool _sessionReady;
    private readonly List<ClickLogPayload> _pendingClicks = new List<ClickLogPayload>();

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
        StartCoroutine(StartSession());
    }

    private IEnumerator StartSession()
    {
        yield return PostJson("/session/start", JsonUtility.ToJson(new SessionStartPayload { sessionId = _sessionId }));

        _sessionReady = true;
        foreach (var payload in _pendingClicks)
        {
            StartCoroutine(PostJson("/log", JsonUtility.ToJson(payload)));
        }
        _pendingClicks.Clear();
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

    private void LogClick(Vector2 screenPosition)
    {
        var payload = new ClickLogPayload
        {
            sessionId = _sessionId,
            timestamp = DateTime.UtcNow.ToString("o"),
            objectName = ResolveClickedObjectName(screenPosition),
        };

        if (_sessionReady)
        {
            StartCoroutine(PostJson("/log", JsonUtility.ToJson(payload)));
        }
        else
        {
            _pendingClicks.Add(payload);
        }
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
    }

    [Serializable]
    private class ClickLogPayload
    {
        public string sessionId;
        public string timestamp;
        public string objectName;
    }
}
