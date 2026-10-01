using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResponseReview : MonoBehaviour
{
    public TextMeshProUGUI reviewText;

    // Text colours. Group 2's text sits on a paper panel (TextBoxTheme), so
    // headings are the panel's indigo and the rest ink instead of black and white.
    private static string Head => TextBoxTheme.Enabled ? "#4A5494" : "#000000";
    private static string Body => TextBoxTheme.Enabled ? "#1F2233" : "#FFFFFF";

    void Start()
    {
        int slot = PlayerPrefs.GetInt("SelectedSaveSlot", 0);
        Debug.Log($"[ResponseReview] Loading from slot {slot}. HasSave: {SaveManager.HasSave(slot)}");

        if (!SaveManager.HasSave(slot))
        {
            reviewText.text = $"<b><color={Head}>No saved responses found.</color></b>";
            return;
        }

        SaveData data = SaveManager.Load(slot);

        if (data.responses == null || data.responses.Count == 0)
        {
            reviewText.text = $"<b><color={Head}>No responses submitted in this save.</color></b>";
            Debug.Log("[ResponseReview] No responses found in this save.");
            return;
        }

        reviewText.text = "";
        for (int i = 0; i < data.responses.Count; i++)
        {
            var r = data.responses[i];
            reviewText.text +=
                $"<b><color={Head}>Scenario {i + 1}:</color></b> <color={Body}>{r.scenario}</color>\n\n" +
                $"<b><color={Head}>Response:</color></b> <color={Body}>{r.response}</color>\n\n" +
                $"<b><color={Head}>AI Feedback:</color></b> <color={Body}>{r.aiFeedback}</color>\n\n\n";
        }

        Debug.Log($"[ResponseReview] Displayed {data.responses.Count} responses.");
    }
}
