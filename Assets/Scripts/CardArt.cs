using System;
using UnityEngine;

// The art for the playing cards (Feature.NewCardArt): one back and front per
// area of psychology. Each dealt card wears the art of its term's area (see
// TermAreas), picked at random when the term belongs to several. Lives at
// Assets/Resources/CardArt.asset; swap art in by dragging sprites into an
// area's layer lists, no code or scene changes needed.
//
// Each side is a stack of layers, bottom layer first. Every layer is
// stretched over the whole card, so export each layer on the full card
// canvas (transparent where it shouldn't cover anything). The card takes the
// shape of the first back layer, so the art is never squashed. Front layers
// are drawn behind the term text and the Definition button.
[CreateAssetMenu(menuName = "PsychGame/Card Art", fileName = "CardArt")]
public class CardArt : ScriptableObject
{
    public const string ResourcePath = "CardArt";

    [Serializable]
    public class Layer
    {
        public Sprite sprite;

        [Tooltip("Gently drifts on its own on top of the layers below (e.g. the card back's icon).")]
        public bool drift;
    }

    [Serializable]
    public class AreaArt
    {
        public PsychArea area;

        [Tooltip("The face-down side, bottom layer first.")]
        public Layer[] backLayers;

        [Tooltip("The face-up side (behind the term), bottom layer first.")]
        public Layer[] frontLayers;
    }

    [Tooltip("One entry per area of psychology. The first entry is also used for any term with no area listed.")]
    public AreaArt[] areas;

    public static CardArt Load() => Resources.Load<CardArt>(ResourcePath);

    // The art for an area, falling back to the first entry when the area is
    // unknown (null) or has no entry yet.
    public AreaArt For(PsychArea? area)
    {
        if (areas == null || areas.Length == 0) return null;

        if (area != null)
        {
            foreach (var a in areas)
                if (a != null && a.area == area.Value) return a;
            Debug.LogWarning($"[CardArt] No art for {area.Value}; using {areas[0].area}.");
        }
        return areas[0];
    }
}
