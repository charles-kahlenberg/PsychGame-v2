using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResponseReview : MonoBehaviour
{
    public TextMeshProUGUI reviewText;

    void Start()
    {
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", 0);
        Debug.Log($"[ResponseReview] Loading from slot {slot}. HasSave: {SaveManager.HasSave(slot)}");

        if (!SaveManager.HasSave(slot))
        {
            reviewText.text = "<b><color=#000000>No saved responses found.</color></b>";
            return;
        }

        SaveData data = SaveManager.Load(slot);

        if (data.responses == null || data.responses.Count == 0)
        {
            reviewText.text = "<b><color=#000000>No responses submitted in this save.</color></b>";
            Debug.Log("[ResponseReview] No responses found in this save.");
            return;
        }

        reviewText.text = "";
        for (int i = 0; i < data.responses.Count; i++)
        {
            var r = data.responses[i];
            reviewText.text +=
                $"<b><color=#000000>Scenario {i + 1}:</color></b> <color=#FFFFFF>{r.scenario}</color>\n\n" +
                $"<b><color=#000000>Response:</color></b> <color=#FFFFFF>{r.response}</color>\n\n" +
                $"<b><color=#000000>AI Feedback:</color></b> <color=#FFFFFF>{r.aiFeedback}</color>\n\n\n";
        }

        Debug.Log($"[ResponseReview] Displayed {data.responses.Count} responses.");
    }
}
