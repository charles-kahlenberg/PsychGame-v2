using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.MenuNewGameTransition (Group 2): New Game on the title screen goes
// to the first synopsis without the loading screen, which only ever waited:
//  1. The title slides up and the other buttons slide off to the sides; New
//     Game is left alone for a moment, then slides off too while the centre
//     light fades.
//  2. The card backs drop: each lets go of the grid, falls like a sheet of
//     paper (speeding up to a gentle top speed, swaying and rocking) and
//     drifts over to one side, leaning that way, so they tumble over each
//     other until only the blue is left.
//  3. The synopsis loads under that still blue, so the load's stall shows
//     nothing, with the menu kept over it (Covering). The menu fades away as
//     the synopsis's background comes up from calm to its excited look, and
//     then the synopsis begins (IntroductionManager waits for it).
//
// The falling cards are one mesh (this Graphic) laid over the menu's wall,
// rebuilt each frame like MenuIntro's, drawn with a copy of the wall's
// material in its Loose mode so they look the same. On the frame they let go
// the wall stops drawing its cards (_ShowCards) and its vignette moves to a
// layer above them. Every timing and look knob is a constant below.
public class MenuNewGameTransition : MaskableGraphic
{
    private const float SlideTime = 0.65f;
    private const float SlideStagger = 0.08f;  // between the title and each button setting off
    // New Game alone, after the rest finish moving. They're out of sight
    // ~0.1s before that (easing in, the last stretch is off screen).
    private const float NewGameHold = 0.1f;
    private const float RevealTime = 1.8f;     // the menu fades into the synopsis's background

    // The fall, in screen heights and seconds; "each" values vary per card.
    private const float Gravity = 3f;          // speeding up at this...
    private const float FallSpeed = 0.9f;      // ...to about this, held back by the air
    private const float ReleaseSpread = 0.2f;  // the cards let go up to this long after the first
    private const float Drift = 0.35f;         // speed sideways, all the same way, each 0.6-1.4x
    private const float Lean = 35f;            // degrees each leans the way it drifts, each 0.5-1.5x
    private const float Sway = 0.06f;          // side to side, each 0.6-1.4x this far,
    private const float SwayRate = 4f;         // this fast (radians a second), each 0.7-1.3x,
    private const float SwayRock = 18f;        // rocking this many degrees each way as it swings
    private const float Settle = 0.3f;         // how long (about) the drift and sway take to build
    private const float MaxFallTime = 6f;      // in case a card never leaves

    private const int CoverSortingOrder = 200; // over the intro, below SceneTransition's fade
    private const string NewGameButton = "NewGameButton";

    private static readonly int ShowCardsId = Shader.PropertyToID("_ShowCards");
    private static readonly int LooseId = Shader.PropertyToID("_Loose");
    private static readonly int LightStrengthId = Shader.PropertyToID("_LightStrength");
    private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");

    // True from the click until the menu is gone from over the synopsis.
    public static bool Covering { get; private set; }

    private struct Card
    {
        public Vector2 start;  // its centre on the grid, in screen heights from the screen's centre
        public int atlasIndex;
        public float release, drift, lean, sway, swayRate, swayPhase;
    }

    private Material _wall;
    private Card[] _cards;
    private Vector2 _cardHalfSize; // screen heights
    private int _atlasCards;
    private float _side;           // 1 right, -1 left
    private float _fallTime;
    private Texture2D _vignetteTexture;

    // Starts it on the title screen. False when it can't (another group, or
    // the menu's cards aren't there): load the old way.
    public static bool Play(Scene menu)
    {
        if (!TestGroups.IsEnabled(Feature.MenuNewGameTransition)) return false;
        if (Covering) return true; // already on its way

        foreach (GameObject root in menu.GetRootGameObjects())
        {
            if (root.GetComponent<Canvas>() == null) continue;
            Transform panel = root.transform.Find("Panel");
            if (panel == null || panel.GetComponent<MenuCardBackground>() == null) continue;

            Covering = true;
            var falling = Layer<MenuNewGameTransition>(panel, "FallingCards");
            falling.raycastTarget = false;
            falling._wall = panel.GetComponent<Image>().material;
            falling.StartCoroutine(falling.Run(root.transform));
            return true;
        }
        return false;
    }

    // A full-screen layer just above the wall. CanvasRenderer listed
    // outright, as in MenuIntro.Layer: Unity doesn't add it to a runtime-added Graphic.
    private static T Layer<T>(Transform wall, string name) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rt = (RectTransform)go.transform;
        rt.SetParent(wall.parent, false);
        rt.SetSiblingIndex(wall.GetSiblingIndex() + 1);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.AddComponent<T>();
    }

    private IEnumerator Run(Transform canvas)
    {
        GradingTransition.BlockClicks(canvas);
        Vector2 size = ((RectTransform)canvas).rect.size;

        // 1. The buttons' layout would pull them back mid-slide. Its size
        // fitter goes first: with no layout the container shrank to nothing,
        // and the buttons, pinned to its corner, jumped down and right.
        Transform container = canvas.Find("ButtonContainer");
        if (container != null)
        {
            if (container.TryGetComponent(out ContentSizeFitter fitter)) fitter.enabled = false;
            if (container.TryGetComponent(out LayoutGroup layout)) layout.enabled = false;
        }

        float delay = 0f;
        Slide(canvas.Find("Title"), Vector2.up * size.y, delay);
        Transform newGame = null;
        if (container != null)
        {
            int side = 0;
            foreach (Button button in container.GetComponentsInChildren<Button>())
            {
                if (button.name == NewGameButton)
                {
                    newGame = button.transform;
                    continue;
                }
                delay += SlideStagger;
                Slide(button.transform, (side++ % 2 == 0 ? Vector2.right : Vector2.left) * size.x, delay);
            }
        }
        yield return new WaitForSeconds(delay + SlideTime + NewGameHold);

        // New Game leaves as the light goes; the falling cards aren't lit.
        Slide(newGame, Vector2.left * size.x, 0f);
        float light = _wall.GetFloat(LightStrengthId);
        for (float t = 0f; t < SlideTime; t += Time.deltaTime)
        {
            _wall.SetFloat(LightStrengthId, light * (1f - Mathf.SmoothStep(0f, 1f, t / SlideTime)));
            yield return null;
        }
        _wall.SetFloat(LightStrengthId, 0f);

        // 2. All in one frame: the wall's cards become these, in the same places.
        LetGo();
        while (_cards != null)
        {
            yield return null;
            _fallTime += Time.deltaTime;
            if (AllGone() || _fallTime > MaxFallTime) _cards = null;
            SetVerticesDirty();
        }
        yield return null; // drawn empty before the load

        // 3. The menu, now just its blue, stays over the synopsis.
        canvas.GetComponent<Canvas>().sortingOrder = CoverSortingOrder;
        DontDestroyOnLoad(canvas.gameObject);
        SceneTransition.Cut(SynopsisTransition.SynopsisScene());

        // Wait out the loads (the intro comes a frame after GameScene), and
        // then the long frame they made, so the fade starts smoothly.
        Scene intro;
        do
        {
            ShaderBackground.Excite = 0f;
            yield return null;
            intro = SceneManager.GetSceneByName("IntroductionScene");
        } while (!intro.isLoaded);
        ShaderBackground.Excite = 0f;
        yield return null;

        // Unity's missing components aren't C# null, so no ?? here.
        if (!canvas.TryGetComponent(out CanvasGroup group)) group = canvas.gameObject.AddComponent<CanvasGroup>();
        for (float t = 0f; t < RevealTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / RevealTime);
            group.alpha = 1f - k;
            ShaderBackground.Excite = k;
            yield return null;
        }
        ShaderBackground.Excite = 1f;
        Covering = false;
        Destroy(canvas.gameObject);
    }

    private static void Slide(Transform t, Vector2 by, float delay)
    {
        if (t == null) return;
        LeanTween.cancel(t.gameObject);
        LeanTween.moveLocal(t.gameObject, t.localPosition + (Vector3)by, SlideTime).setDelay(delay).setEaseInCubic();
    }

    // -------------------- THE FALL --------------------

    private void LetGo()
    {
        var atlas = (Texture2D)_wall.GetTexture("_CardTex");
        _atlasCards = Mathf.RoundToInt(_wall.GetFloat("_CardCount"));
        float cardHeight = _wall.GetFloat("_CardHeight");
        float gap = _wall.GetFloat("_Gap");
        float speed = _wall.GetFloat("_Speed");
        float stagger = _wall.GetFloat("_Stagger");
        float cardWidth = atlas.width / (float)(_atlasCards * atlas.height);

        material = new Material(_wall) { name = "MenuCards Loose (runtime)" };
        material.SetFloat(LooseId, 1f);
        _cardHalfSize = 0.5f * cardHeight * new Vector2(cardWidth, 1f);
        _side = Random.value < 0.5f ? 1f : -1f;

        // Every card on screen, worked out as MenuCards.shader does, in card
        // heights from the screen's bottom left. Its clock is the time since
        // the scene loaded.
        Rect rect = rectTransform.rect;
        float aspect = rect.width / rect.height;
        float width = aspect / cardHeight, height = 1f / cardHeight;
        Vector2 cell = new Vector2(cardWidth, 1f) + Vector2.one * gap;
        float time = Time.timeSinceLevelLoad;

        var cards = new System.Collections.Generic.List<Card>();
        for (int column = 0; column * cell.x < width; column++)
        {
            float direction = column % 2 == 0 ? 1f : -1f;
            float offset = direction * speed * time + column * stagger * cell.y;
            int lastRow = Mathf.CeilToInt((height + offset) / cell.y);
            for (int row = Mathf.FloorToInt((offset - 1f) / cell.y); row <= lastRow; row++)
            {
                float bottom = row * cell.y + 0.5f * gap - offset;
                if (bottom >= height || bottom + 1f <= 0f) continue;
                cards.Add(new Card
                {
                    start = new Vector2((column + 0.5f) * cell.x * cardHeight - 0.5f * aspect,
                                        ((row + 0.5f) * cell.y - offset) * cardHeight - 0.5f),
                    atlasIndex = CardAt(column, row),
                    release = Random.Range(0f, ReleaseSpread),
                    drift = Drift * Random.Range(0.6f, 1.4f),
                    lean = Lean * Random.Range(0.5f, 1.5f),
                    sway = Sway * Random.Range(0.6f, 1.4f),
                    swayRate = SwayRate * Random.Range(0.7f, 1.3f),
                    swayPhase = Random.Range(0f, 2f * Mathf.PI),
                });
            }
        }

        // Each on a random layer, so they fall over each other every which way.
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
        _cards = cards.ToArray();
        SetVerticesDirty();

        // The wall's vignette darkened its cards too, so it goes over these.
        _wall.SetFloat(ShowCardsId, 0f);
        float vignette = _wall.GetFloat(VignetteStrengthId);
        _wall.SetFloat(VignetteStrengthId, 0f);
        _vignetteTexture = MenuIntro.VignetteTexture(_wall.GetColor("_VignetteColor"),
            _wall.GetFloat("_VignetteStart"), _wall.GetFloat("_VignetteSoftness"), vignette);
        var overlay = Layer<RawImage>(transform, "Vignette");
        overlay.raycastTarget = false;
        overlay.texture = _vignetteTexture;
    }

    // Where a card is, and its turn in degrees, this long after it let go.
    private void Place(Card c, float age, out Vector2 position, out float rotation)
    {
        // Gravity against the air's drag: soon falling at a steady speed.
        float fall = FallSpeed * age - FallSpeed * FallSpeed / Gravity * (1f - Mathf.Exp(-Gravity * age / FallSpeed));
        // The drift and sway build from rest, so nothing jumps as it lets go
        // (the drift's distance is its speed built up the same way).
        float build = 1f - Mathf.Exp(-age / Settle);
        float swing = c.swayRate * age + c.swayPhase;
        position = c.start + new Vector2(_side * c.drift * (age - Settle * build) + c.sway * build * Mathf.Sin(swing), -fall);
        // Leaning, and dipping, the way it's heading.
        rotation = -build * (_side * c.lean + SwayRock * Mathf.Cos(swing));
    }

    private bool AllGone()
    {
        Rect rect = rectTransform.rect;
        float halfWidth = 0.5f * rect.width / rect.height, reach = _cardHalfSize.magnitude;
        foreach (Card c in _cards)
        {
            Place(c, Mathf.Max(0f, _fallTime - c.release), out Vector2 p, out _);
            if (p.y + reach > -0.5f && Mathf.Abs(p.x) - reach < halfWidth) return false;
        }
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_cards == null) return;

        Rect rect = rectTransform.rect;
        Vector2 halfSize = _cardHalfSize * rect.height;
        foreach (Card c in _cards)
        {
            Place(c, Mathf.Max(0f, _fallTime - c.release), out Vector2 p, out float rotation);
            AddCard(vh, rect.center + p * rect.height, rotation * Mathf.Deg2Rad, halfSize, c.atlasIndex);
        }
    }

    private void AddCard(VertexHelper vh, Vector2 position, float rotation, Vector2 halfSize, int atlasIndex)
    {
        float cos = Mathf.Cos(rotation), sin = Mathf.Sin(rotation);
        Vector2 right = new Vector2(cos, sin) * halfSize.x, up = new Vector2(-sin, cos) * halfSize.y;
        float u0 = atlasIndex / (float)_atlasCards, u1 = (atlasIndex + 1) / (float)_atlasCards;

        int start = vh.currentVertCount;
        Color32 tint = color;
        vh.AddVert(position - right - up, tint, new Vector2(u0, 0f));
        vh.AddVert(position - right + up, tint, new Vector2(u0, 1f));
        vh.AddVert(position + right + up, tint, new Vector2(u1, 1f));
        vh.AddVert(position + right - up, tint, new Vector2(u1, 0f));
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_vignetteTexture != null) Destroy(_vignetteTexture);
    }

    // -------------------- MenuCards.shader's choice of card --------------------
    // CardAt, Pick and Hash as the shader has them (see there), so each
    // falling card is the one that was in its place.

    private int CardAt(float column, float row)
    {
        const float perColumn = 3f;
        float first = 3f * column;

        float slot;
        if (Frac(row * 0.5f) < 0.25f)
        {
            slot = Pick(column, row, perColumn);
        }
        else
        {
            float a = Pick(column, row - 1f, perColumn);
            float b = Pick(column, row + 1f, perColumn);
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            slot = Pick(column, row, perColumn - (a == b ? 1f : 2f));
            if (slot >= lo) slot += 1f;
            if (a != b && slot >= hi) slot += 1f;
        }
        return (int)Mathf.Floor(Frac((first + slot + 0.5f) / _atlasCards) * _atlasCards);
    }

    private static float Pick(float x, float y, float n)
    {
        return Mathf.Min(Mathf.Floor(Hash(x, y) * n), n - 1f);
    }

    private static float Hash(float px, float py)
    {
        float x = Frac(px * 0.1031f), y = Frac(py * 0.1031f), z = x; // p.xyx
        float d = x * (y + 33.33f) + y * (z + 33.33f) + z * (x + 33.33f);
        x += d; y += d; z += d;
        return Frac((x + y) * z);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);
}
