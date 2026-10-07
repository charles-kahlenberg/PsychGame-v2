using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Feature.MenuIntro (Group 2): when the game opens on the title screen, it
// plays an intro over it before the menu shows:
//  1. Just the menu's dark blue.
//  2. Hundreds of card backs stream in from all round the edges of the
//     screen, in a score of lines: each card follows the one ahead along
//     the same slightly swirling path, spinning, and slowing to a stop as
//     it lands, so the pile is a mess of every line's angle. A trickle at
//     first, then exponentially more, then a few stragglers.
//  3. The pile charges up: it draws in tighter and tighter while a handful
//     of the cards on top shake harder and harder and the screen dims. Then it bursts outwards
//     as the screen flashes white, and the white fades away to show the menu.
// A click anywhere skips to the white, which then fades faster to the menu.
// Once a session, on the first scene only: back at the menu later, there's
// no intro. ClickLogger holds the username prompt until it's done (Playing).
//
// The cards are one mesh (this Graphic) on the intro's own canvas, rebuilt
// each frame: one draw call from the menu's card atlas, and the menu's
// canvas isn't touched. The colour and atlas come from the menu's shader, so
// they always match it. Every timing and look knob is a constant below.
public class MenuIntro : MaskableGraphic
{
    private const int CardCount = 192;     // sets the horde's peak: 80 cards in the busiest half second
    private const int StreamCount = 22;       // lines of cards, from all round the edges but unevenly spaced
    private const float StreamJitter = 1.5f;  // degrees each way a card strays from its line
    private const float CardHeight = 0.15f;   // landed, in screen heights
    private const float StartScale = 1.2f;    // flying in from "nearer the camera"
    private const float Mess = 0.07f;         // how far from the centre cards land, in screen heights
    private const float Swirl = 75f;          // degrees each card circles the centre on the way in
    private const float SpinTurns = 1.5f;     // whole turns each card spins on its way in, slowing as it lands
    private const float SwirlRadians = Swirl * Mathf.Deg2Rad;

    private const float BlankTime = 0.35f;    // plain blue before the first card
    private const float LaunchSpread = 4.5f;  // cards set off over this long: a trickle, then a horde, then a few
    private const float RatePeak = 0.75f;     // the horde peaks this far through the launches
    private const float RateRise = 8f;        // how sharply it builds (exponentially) to the peak...
    private const float RateFall = 12f;       // ...and how sharply it drops off after
    private const float FlightTime = 1.8f;    // the same for every card, so a line stays evenly spaced
    private const float Charge = 2.2f;        // the pile charges up over the last this-long of the arrivals, and
                                              // bursts the instant the last card lands:
    private const float ChargePull = 0.7f;    // drawing in this much tighter (0 none, 1 to a point),
    private const int ShakingCards = 20;      // while this many cards on top shake,
    private const float ShakeSize = 0.022f;   // up to this far (screen heights)
    private const float ShakeTilt = 9f;       // and this many degrees each way,
    private const float ShakeSpeed = 90f;     // this fast (radians a second),
    private const float IntroVignette = 0.95f; // the menu's vignette, but this strong (the menu's is 0.8)
    private const float ChargeDim = 0.8f;     // and the screen dims to this (0 none, 1 black) at the burst,
    private const float DimCurve = 4f;        // building exponentially (higher: later and steeper)
    private const float BurstTime = 0.5f;     // a typical card's burst, each one 0.6-1.6x this,
    private const float BurstStagger = 0.12f; // setting off up to this long after the first
    private const float BurstMin = 0.3f, BurstMax = 2.6f; // how far cards fly out, in screen heights
    private const float FlashDelay = 0.15f;   // after the burst starts
    private const float FlashIn = 0.55f;
    private const float WhiteHold = 0.3f;
    private const float FadeOut = 1.4f;
    private const float SkipFadeOut = 0.6f;   // the white's fade after a click skips the intro

    private const int SortingOrder = 900; // over the scene, under SceneTransition's fade
    private const string ShaderResource = "MenuCards";
    private const string AtlasResource = "MenuCardBacks";
    // The atlas cells holding the seven area backs; the question card (1, 4, 7) is left out of the intro.
    private static readonly int[] AreaCards = { 0, 2, 3, 5, 6, 8, 9 };

    // True from the title screen loading until the white has faded.
    public static bool Playing { get; private set; }
    private static bool _checkedFirstScene;

    private struct Card
    {
        public float launch, flight;
        public float startAngle;                           // radians
        public float spinDirection;                        // 1 or -1, the same down a stream
        public Vector2 rest;                               // screen heights from the centre
        public int atlasIndex;
        public Vector2 burstDirection;
        public float burstDistance, burstSpin, burstDelay, burstTime;
        public float shakePhase;
    }

    private Card[] _cards;
    private Texture2D _atlas;
    private int _atlasCards;
    private float _cardAspect;
    private Image _background, _dim, _flash;
    private RawImage _vignette;
    private Texture2D _vignetteTexture;
    private float _time;
    private float _chargeStart, _burstStart, _whiteEnd;
    private float _fadeOut = FadeOut;

    public override Texture mainTexture => _atlas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TestGroups.IsEnabled(Feature.MenuIntro)) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (_checkedFirstScene) return;
            _checkedFirstScene = true;
            if (MenuCardBackground.Replaces(scene)) Play();
        };
    }

    private static void Play()
    {
        var shader = Resources.Load<Shader>(ShaderResource);
        var atlas = Resources.Load<Texture2D>(AtlasResource);
        if (shader == null || atlas == null) return;
        var menuLook = new Material(shader);
        Color blue = menuLook.GetColor("_BackColor");
        int atlasCards = Mathf.RoundToInt(menuLook.GetFloat("_CardCount"));
        Texture2D vignetteTexture = VignetteTexture(menuLook.GetColor("_VignetteColor"),
            menuLook.GetFloat("_VignetteStart"), menuLook.GetFloat("_VignetteSoftness"), IntroVignette);
        Destroy(menuLook);

        var go = new GameObject("MenuIntro");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        go.AddComponent<GraphicRaycaster>(); // the background and flash block the menu's buttons

        var background = Layer<Image>(go.transform, "Background");
        background.color = blue;

        var cards = Layer<MenuIntro>(go.transform, "Cards");
        cards.raycastTarget = false;
        cards._atlas = atlas;
        cards._atlasCards = atlasCards;
        cards._cardAspect = atlas.width / (float)atlasCards / atlas.height;
        cards._background = background;

        var vignette = Layer<RawImage>(go.transform, "Vignette");
        vignette.raycastTarget = false;
        vignette.texture = vignetteTexture;
        cards._vignette = vignette;
        cards._vignetteTexture = vignetteTexture;

        var dim = Layer<Image>(go.transform, "Dim");
        dim.color = new Color(0f, 0f, 0f, 0f);
        cards._dim = dim;

        var flash = Layer<Image>(go.transform, "Flash");
        flash.color = new Color(1f, 1f, 1f, 0f);
        cards._flash = flash;

        cards.Deal();
        Playing = true;
    }

    // The menu's vignette (MenuCards.shader, at its default screen-shaped
    // Roundness) as a texture stretched over the screen, over the cards.
    // MenuNewGameTransition lays it over its falling cards too.
    internal static Texture2D VignetteTexture(Color color, float start, float softness, float strength)
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "IntroVignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float fromCentre = (new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.one).magnitude;
            color.a = strength * Mathf.SmoothStep(0f, 1f, (fromCentre - start) / softness);
            pixels[y * size + x] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_vignetteTexture != null) Destroy(_vignetteTexture);
    }

    private static T Layer<T>(Transform parent, string name) where T : Graphic
    {
        // CanvasRenderer listed outright: Graphic asks for one, but on this
        // runtime-added subclass Unity didn't add it, and without one the
        // cards were silently never drawn.
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go.AddComponent<T>();
    }

    // Every card's path, worked out up front.
    private void Deal()
    {
        _cards = new Card[CardCount];
        int perStream = Mathf.CeilToInt(CardCount / (float)StreamCount);
        // One stream in each of StreamCount equal slices of the circle, at a
        // random spot in its slice: every side and corner gets its share,
        // but the gaps between streams vary, so there's no regular pattern.
        // (Fully random angles could leave a whole side empty.)
        var streamAngles = new float[StreamCount];
        var streamPhases = new float[StreamCount]; // so the streams don't launch in step
        var streamSpins = new float[StreamCount];
        float slice = 2f * Mathf.PI / StreamCount, turn = Random.Range(0f, slice);
        for (int s = 0; s < StreamCount; s++)
        {
            streamAngles[s] = turn + slice * (s + 0.5f + Random.Range(-0.4f, 0.4f));
            streamPhases[s] = Random.value;
            streamSpins[s] = Random.value < 0.5f ? 1f : -1f;
        }
        float lastLanding = 0f;
        for (int i = 0; i < CardCount; i++)
        {
            // Card i is the (i / StreamCount)th in line in stream i % StreamCount.
            int stream = i % StreamCount, place = i / StreamCount;
            var c = new Card
            {
                launch = LaunchSpread * Swell((place + streamPhases[stream]) / perStream),
                flight = FlightTime,
                spinDirection = streamSpins[stream],
                startAngle = streamAngles[stream] +
                             Random.Range(-StreamJitter, StreamJitter) * Mathf.Deg2Rad,
                rest = Random.insideUnitCircle * Mess,
                atlasIndex = AreaCards[Random.Range(0, AreaCards.Length)],
                // Spread from near to far (not all at one distance, at one
                // speed, which flew out as a ring).
                burstDistance = Mathf.Lerp(BurstMin, BurstMax, Mathf.Sqrt(Random.value)),
                burstDelay = Random.Range(0f, BurstStagger),
                burstTime = BurstTime * Random.Range(0.6f, 1.6f),
                burstSpin = Random.Range(-360f, 360f) * Mathf.Deg2Rad,
                shakePhase = Random.Range(0f, 2f * Mathf.PI),
            };
            float burstAngle = Mathf.Atan2(c.rest.y, c.rest.x) + Random.Range(-0.5f, 0.5f);
            c.burstDirection = new Vector2(Mathf.Cos(burstAngle), Mathf.Sin(burstAngle));
            _cards[i] = c;
            lastLanding = Mathf.Max(lastLanding, c.launch + c.flight);
        }

        // Each card on a random layer, so the pile comes together mixed.
        for (int i = CardCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }

        _burstStart = BlankTime + lastLanding;
        _chargeStart = _burstStart - Charge;
        _whiteEnd = _burstStart + FlashDelay + FlashIn + WhiteHold;
    }

    // Spreads evenly spaced launches (0-1) over the launch time (0-1)
    // following the launch rate: a trickle that grows exponentially to a
    // horde at RatePeak, then drops off fast. Over 4.5s, with 192 cards,
    // that's about 10 in the first 2 seconds, then 15, 35, 80, 41 and 11
    // per half second. The rate's running total is tabulated and read backwards.
    private static float[] _launchCurve;

    private static float Swell(float x)
    {
        const int steps = 256;
        if (_launchCurve == null)
        {
            _launchCurve = new float[steps + 1];
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float rate = Mathf.Exp(t < RatePeak ? RateRise * (t - RatePeak) : -RateFall * (t - RatePeak));
                _launchCurve[i + 1] = _launchCurve[i] + rate;
            }
            for (int i = 1; i <= steps; i++) _launchCurve[i] /= _launchCurve[steps];
        }

        for (int i = 0; i < steps; i++)
        {
            if (_launchCurve[i + 1] < x) continue;
            return (i + Mathf.InverseLerp(_launchCurve[i], _launchCurve[i + 1], x)) / steps;
        }
        return 1f;
    }

    // From the centre to the screen's edge at this angle, in screen heights.
    private static float EdgeDistance(float angle, float aspect)
    {
        float cos = Mathf.Abs(Mathf.Cos(angle)), sin = Mathf.Abs(Mathf.Sin(angle));
        return Mathf.Min(cos > 1e-4f ? 0.5f * aspect / cos : float.MaxValue,
                         sin > 1e-4f ? 0.5f / sin : float.MaxValue);
    }

    private void Update()
    {
        // A hitch (the first frames after loading) slows the intro rather
        // than skipping part of it.
        _time += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        // A click (or tap) skips to the white.
        if (_time < _whiteEnd && Input.GetMouseButtonDown(0))
        {
            _time = _whiteEnd;
            _fadeOut = SkipFadeOut;
        }

        float flashUp = Mathf.SmoothStep(0f, 1f, (_time - _burstStart - FlashDelay) / FlashIn);
        if (_time < _whiteEnd)
        {
            SetAlpha(_dim, ChargeDim * (Mathf.Exp(DimCurve * ChargeProgress) - 1f) / (Mathf.Exp(DimCurve) - 1f));
            SetAlpha(_flash, flashUp);
            SetVerticesDirty();
            return;
        }

        // Under full white, the menu takes the place of the blue and cards.
        if (_background.enabled)
        {
            _background.enabled = false;
            _vignette.enabled = false;
            _dim.enabled = false;
            _cards = null;
            SetVerticesDirty();
        }
        float fade = (_time - _whiteEnd) / _fadeOut;
        SetAlpha(_flash, 1f - Mathf.SmoothStep(0f, 1f, fade));
        if (fade >= 1f)
        {
            Playing = false;
            Destroy(transform.parent.gameObject);
        }
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    // 0 until the charge-up starts, rising steadily to 1 at the burst.
    private float ChargeProgress => Mathf.Clamp01((_time - _chargeStart) / Charge);

    // The same, building faster and faster.
    private float ChargeLevel => ChargeProgress * ChargeProgress;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_cards == null) return;

        // The screen's shape comes from this canvas as it's drawn: read
        // from Screen as the scene loaded, it could still be 0 x 0 in the
        // Editor, and every card's path came out NaN (invisible).
        Rect rect = rectTransform.rect;
        float screenHeight = rect.height;
        if (screenHeight <= 0f) return;
        float aspect = rect.width / screenHeight;
        Vector2 centre = rect.center;
        float halfDiagonal = 0.5f * CardHeight * StartScale * Mathf.Sqrt(1f + _cardAspect * _cardAspect);
        Vector2 halfSize = 0.5f * screenHeight * CardHeight * new Vector2(_cardAspect, 1f);
        // Charging builds up faster and faster, then the burst takes over.
        float charge = ChargeLevel;
        float pull = 1f - ChargePull * charge;
        float shake = charge * (1f - Mathf.Clamp01((_time - _burstStart) / (0.25f * BurstTime)));

        for (int i = 0; i < _cards.Length; i++)
        {
            Card c = _cards[i];
            float u = (_time - BlankTime - c.launch) / c.flight;
            if (u <= 0f) continue;
            // Fast in, soft landing: within a pixel of its spot by 85% of the
            // flight, rather than creeping the last few (a cubic still was).
            float e = 1f - Mathf.Pow(1f - Mathf.Min(u, 1f), 4f);

            // Circling in from just past the screen's edge to its spot on the pile.
            float startRadius = EdgeDistance(c.startAngle, aspect) + halfDiagonal;
            float angle = c.startAngle + SwirlRadians * e;
            Vector2 position = startRadius * (1f - e) * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) + c.rest * e;
            // Top towards the centre as it swirls, plus its own spin, which
            // carries on all the way in, slowing with the card to stop as it
            // lands. Every card in a stream spins the same way at the same
            // point, so a line's cards still match.
            float rotation = c.startAngle + SwirlRadians * e + 0.5f * Mathf.PI +
                             c.spinDirection * SpinTurns * 2f * Mathf.PI * (1f - e);
            float scale = Mathf.Lerp(StartScale, 1f, e);

            // The pile charges up, then bursts. The cards drawn last are on
            // top, so those are the ones seen shaking.
            float burst = 1f - Mathf.Pow(1f - Mathf.Clamp01((_time - _burstStart - c.burstDelay) / c.burstTime), 3f);
            position = position * pull + c.burstDirection * c.burstDistance * burst;
            rotation += c.burstSpin * burst;
            scale *= Mathf.Lerp(1f, 0.94f, charge) + 0.6f * burst;
            if (i >= _cards.Length - ShakingCards && shake > 0f)
            {
                float wave = _time * ShakeSpeed + c.shakePhase;
                position += ShakeSize * shake * new Vector2(Mathf.Sin(wave), Mathf.Sin(1.37f * wave + 2f));
                rotation += ShakeTilt * Mathf.Deg2Rad * shake * Mathf.Sin(0.83f * wave + 4f);
            }

            AddCard(vh, centre + position * screenHeight, rotation, halfSize * scale, c.atlasIndex);
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
}
