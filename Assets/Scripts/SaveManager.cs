using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public string title;
    public string scenario;
    public List<string> cards;
    public List<string> usedScenarios;
    public List<string> usedVocab;
    public List<ScenarioResponse> responses;

    // New field for storing NPC intros per scenario
    public Dictionary<string, string> npcIntroductions = new Dictionary<string, string>();
}



[System.Serializable]
public class ScenarioResponse
{
    public string scenario;
    public string response;
    public string aiFeedback;
}


public static class SaveManager
{
    public static string GetSaveKey(int index) => $"SaveSlot_{index}";
    public static string GetTitleKey(int index) => $"SaveSlot_{index}_Title";

    public static bool HasSave(int slot)
    {
        return PlayerPrefs.HasKey(GetSaveKey(slot));
    }

    public static SaveData Load(int index)
    {
        string json = PlayerPrefs.GetString(GetSaveKey(index), "");
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static void Save(int index, SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(GetSaveKey(index), json);
        PlayerPrefs.SetString(GetTitleKey(index), data.title);
        PlayerPrefs.SetInt("HasSave", 1);
        PlayerPrefs.Save();

        // DEBUG LINE
        Debug.Log($"[SaveManager] Slot {index} saved with title: {data.title}");
    }


    public static void Delete(int index)
    {
        PlayerPrefs.DeleteKey(GetSaveKey(index));
        PlayerPrefs.DeleteKey(GetTitleKey(index));
    }

    public static string GetTitle(int index) =>
        PlayerPrefs.GetString(GetTitleKey(index), "Empty");

    private static int tempSaveSlot = -1;

    public static void SetTempSaveSlot(int slot)
    {
        tempSaveSlot = slot;
    }

    public static int GetTempSaveSlot()
    {
        return tempSaveSlot;
    }
    public static int GetFirstAvailableSlot()
    {
        for (int i = 0; i < 3; i++)
        {
            if (!HasSave(i))
                return i;
        }

        // All slots full ? overwrite slot 0 (or handle however you want)
        return 0;
    }

}