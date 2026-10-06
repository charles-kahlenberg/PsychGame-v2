using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// The one way the game talks to its Cloudflare Workers (intro, hints,
// grading, logging): POST some JSON, get the reply's body back.
public static class Worker
{
    // Hands onDone the response body, or null when the request failed.
    public static IEnumerator Post(string url, string json, Action<string> onDone, int timeoutSeconds = 20)
    {
        using (var req = UnityWebRequest.Post(url, json, "application/json"))
        {
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Worker] {url} failed: {req.error}\n{req.downloadHandler.text}");
                onDone(null);
                yield break;
            }
            onDone(req.downloadHandler.text);
        }
    }

    // Every worker replies with one of these fields, or with "error".
    [Serializable]
    public class Reply
    {
        public string intro, hint, feedback, error;
    }

    // The reply's `field`, else its error, else null (also for unparseable JSON).
    public static string Read(string json, Func<Reply, string> field)
    {
        try
        {
            Reply r = JsonUtility.FromJson<Reply>(json);
            if (r == null) return null;
            if (!string.IsNullOrWhiteSpace(field(r))) return field(r);
            return string.IsNullOrWhiteSpace(r.error) ? null : r.error;
        }
        catch
        {
            return null;
        }
    }

    // The non-blank cards, trimmed: from a list, or from LastCards' "a|b|c".
    public static List<string> Cards(IEnumerable<string> cards) =>
        cards == null ? new List<string>() : cards.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();

    public static List<string> Cards(string pipeList) => Cards(pipeList?.Split('|'));
}
