using UnityEngine;

public class ResetSave : MonoBehaviour
{
    public void DeleteSave()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("All save data deleted.");
    }
}
