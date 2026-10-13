using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The game's sound effects (Resources/Sounds), played through one shared
// AudioSource that outlives scene loads, so a button that changes scene still
// clicks and a hand dealing in can overlap its sounds.
//
// Every button in each scene clicks, added at runtime like ButtonHoverPop.
// Cards have their own sounds and the dictionary stays quiet. Buttons built
// in code after the scene loads call Sfx.Button themselves.
public static class Sfx
{
    private static AudioSource _source;
    private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

    public static void CardFlip() => Play("CardFlip");
    public static void CardDeal() => Play("CardDeal");
    public static void CardFan() => Play("CardFan");
    public static void Button() => Play("Button");

    private static void Play(string name)
    {
        if (_source == null)
        {
            _source = new GameObject("Sfx").AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(_source.gameObject);
        }
        if (!_clips.TryGetValue(name, out AudioClip clip))
            _clips[name] = clip = Resources.Load<AudioClip>("Sounds/" + name);
        _source.PlayOneShot(clip);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (UnityEngine.UI.Button button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.GetComponent<CardBehavior>() != null) continue;
                if (button.GetComponent<Canvas>() != null) continue; // a whole-screen click catcher, not a real button
                if (button.GetComponentInParent<TermDictionary>(true) != null) continue;
                button.onClick.AddListener(Button);
            }
        };
    }
}
