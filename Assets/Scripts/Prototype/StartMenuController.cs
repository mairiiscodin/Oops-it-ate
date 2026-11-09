using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using OopsItAte.Audio;
using OopsItAte.Levels;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private Image bannerImage;
    [SerializeField] private Sprite[] bannerFrames;
    [SerializeField] private float framesPerSecond = 10f;
    [SerializeField] private RectTransform bannerTransform;
    [SerializeField] private float bobAmount = 10f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private RectTransform startButtonTransform;
    [SerializeField, Min(1)] private int pressesToStart = 3;
    [SerializeField, Min(0f)] private float scalePerPress = 0.2f;
    [SerializeField, Min(0f)] private float finalPressDelay = 0.12f;
    [SerializeField] private string firstSceneName = "IntroCutscene";

    [Header("Pixel Button")]
    [Tooltip("Screen size of one button art pixel, in canvas units.")]
    [SerializeField, Min(1f)] private float buttonPixelScale = 8f;

    [Header("Card Wobble")]
    [SerializeField] private float idleSwayDegrees = 2.5f;
    [SerializeField] private float idleSwaySpeed = 1.6f;
    [Tooltip("Tilt the button snaps to instantly on a press, before springing back.")]
    [SerializeField] private float pressSnapTiltDegrees = 14f;
    [Tooltip("Extra scale the button pops to instantly on a press.")]
    [SerializeField] private float pressSnapPop = 0.22f;
    [SerializeField] private float snapScaleStiffness = 1100f;
    [SerializeField] private float snapScaleDamping = 38f;
    [SerializeField] private float snapTiltStiffness = 950f;
    [SerializeField] private float snapTiltDamping = 30f;

    [Header("Final Press Slam")]
    [Tooltip("How fast the button keeps growing on the final press until it hits the banner.")]
    [SerializeField] private float slamRamSpeed = 24f;
    [SerializeField] private float maxSlamScale = 6f;
    [Tooltip("Fraction of the banner's drawn height, from its bottom edge, that is treated as empty space.")]
    [SerializeField, Range(0f, 0.5f)] private float bannerHitInset = 0.22f;
    [Tooltip("Real-time freeze on impact so the hit reads clearly.")]
    [SerializeField, Min(0f)] private float impactHitStop = 0.07f;
    [SerializeField] private float bannerKickUpSpeed = 2800f;
    [SerializeField] private float bannerKickSideSpeed = 1200f;
    [SerializeField] private float bannerFallGravity = 5600f;
    [SerializeField] private float bannerKickSpin = 1500f;
    [SerializeField] private float bannerImpactSquash = 0.25f;
    [SerializeField, Min(0f)] private float bannerFlightTime = 0.75f;

    private const float BannerBaseY = 90f;

    private int startPressCount;
    private bool isStarting;
    private bool isSlamming;
    private Vector3 startButtonInitialScale = Vector3.one;

    private float scaleTarget = 1f;
    private float scaleCurrent = 1f;
    private float scaleVelocity;
    private float angleCurrent;
    private float angleVelocity;
    private float wobbleSign = 1f;

    private bool bannerLaunched;
    private Vector2 bannerVelocity;
    private float bannerAngularVelocity;
    private float bannerAngle;
    private float bannerSquash;
    private float bannerSquashVelocity;
    private Vector3 bannerInitialScale = Vector3.one;
    private float hitStopTimer;

    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        GameSession.StartNewGame();
        if (startButtonTransform != null)
        {
            startButtonInitialScale = startButtonTransform.localScale;
            ApplyPixelButtonStyle();
        }

        if (bannerTransform != null)
        {
            bannerInitialScale = bannerTransform.localScale;
        }
    }

    private void Update()
    {
        float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        if (bannerImage != null && bannerFrames != null && bannerFrames.Length > 0)
        {
            int frameIndex = Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % bannerFrames.Length;
            bannerImage.sprite = bannerFrames[frameIndex];
        }

        // Hit-stop: everything holds still for a beat on impact.
        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            return;
        }

        // The springs are stiff, so integrate in small steps to keep them stable at
        // low frame rates.
        int steps = Mathf.CeilToInt(deltaTime / (1f / 240f));
        float stepTime = deltaTime / Mathf.Max(1, steps);
        for (int i = 0; i < steps; i++)
        {
            UpdateBanner(stepTime);
            UpdateButton(stepTime);
        }

        if (!bannerLaunched && ButtonTouchesBanner())
        {
            LaunchBanner();
        }
    }

    public void StartGame()
    {
        if (isStarting)
        {
            return;
        }

        startPressCount++;
        scaleTarget = 1f + scalePerPress * startPressCount;

        // Balatro-style card juice: the button snaps instantly to a popped size and
        // a hard tilt (alternating sides), then a stiff, well-damped spring snaps it
        // back with a single short overshoot instead of a long floppy wobble.
        scaleCurrent = scaleTarget + pressSnapPop;
        scaleVelocity = 0f;
        angleCurrent = wobbleSign * pressSnapTiltDegrees * Random.Range(0.9f, 1.1f);
        angleVelocity = 0f;
        wobbleSign = -wobbleSign;
        SoundManager.Play(Sfx.Bump, 1f + 0.12f * startPressCount);

        if (startPressCount >= pressesToStart)
        {
            isStarting = true;
            isSlamming = true;
            Button startButton = startButtonTransform != null
                ? startButtonTransform.GetComponent<Button>()
                : null;
            if (startButton != null)
            {
                startButton.interactable = false;
            }

            StartCoroutine(LoadFirstSceneAfterDelay());
        }
    }

    private void UpdateButton(float deltaTime)
    {
        if (startButtonTransform == null)
        {
            return;
        }

        if (isSlamming && !bannerLaunched)
        {
            scaleTarget = Mathf.Min(scaleTarget + slamRamSpeed * deltaTime, maxSlamScale);
        }

        float scaleAcceleration = (scaleTarget - scaleCurrent) * snapScaleStiffness - scaleVelocity * snapScaleDamping;
        scaleVelocity += scaleAcceleration * deltaTime;
        scaleCurrent += scaleVelocity * deltaTime;

        float time = Time.unscaledTime * idleSwaySpeed;
        float idleAngle = (Mathf.Sin(time) + 0.35f * Mathf.Sin(time * 2.3f + 1.1f)) * idleSwayDegrees;
        float angleAcceleration = (idleAngle - angleCurrent) * snapTiltStiffness - angleVelocity * snapTiltDamping;
        angleVelocity += angleAcceleration * deltaTime;
        angleCurrent += angleVelocity * deltaTime;

        startButtonTransform.localScale = startButtonInitialScale * Mathf.Max(0.05f, scaleCurrent);
        startButtonTransform.localRotation = Quaternion.Euler(0f, 0f, angleCurrent);
    }

    private void UpdateBanner(float deltaTime)
    {
        if (bannerTransform == null)
        {
            return;
        }

        if (!bannerLaunched)
        {
            Vector2 position = bannerTransform.anchoredPosition;
            position.y = BannerBaseY + Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmount;
            bannerTransform.anchoredPosition = position;
            return;
        }

        bannerVelocity.y -= bannerFallGravity * deltaTime;
        bannerTransform.anchoredPosition += bannerVelocity * deltaTime;
        bannerAngle += bannerAngularVelocity * deltaTime;
        bannerTransform.localRotation = Quaternion.Euler(0f, 0f, bannerAngle);

        // Impact squash (wide and flat) that snaps back to normal proportions.
        float squashAcceleration = -bannerSquash * 1200f - bannerSquashVelocity * 40f;
        bannerSquashVelocity += squashAcceleration * deltaTime;
        bannerSquash += bannerSquashVelocity * deltaTime;
        bannerTransform.localScale = new Vector3(
            bannerInitialScale.x * (1f + bannerSquash),
            bannerInitialScale.y * (1f - bannerSquash),
            bannerInitialScale.z);
    }

    private bool ButtonTouchesBanner()
    {
        if (startButtonTransform == null || bannerTransform == null)
        {
            return false;
        }

        startButtonTransform.GetWorldCorners(corners);
        Rect button = BoundsOf(corners);
        Rect banner = GetBannerDrawnWorldRect();
        float visibleBottom = banner.yMin + banner.height * bannerHitInset;
        return button.yMax >= visibleBottom
            && button.xMax >= banner.xMin
            && button.xMin <= banner.xMax;
    }

    // The banner image keeps its aspect ratio, so the art usually covers only part
    // of the RectTransform; work out the area it is actually drawn in.
    private Rect GetBannerDrawnWorldRect()
    {
        bannerTransform.GetWorldCorners(corners);
        Rect rect = BoundsOf(corners);
        Sprite sprite = bannerImage != null ? bannerImage.sprite : null;
        if (bannerImage == null || !bannerImage.preserveAspect || sprite == null || sprite.rect.height <= 0f)
        {
            return rect;
        }

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float rectAspect = rect.width / Mathf.Max(0.0001f, rect.height);
        if (spriteAspect > rectAspect)
        {
            float height = rect.width / spriteAspect;
            return new Rect(rect.xMin, rect.center.y - height * 0.5f, rect.width, height);
        }

        float width = rect.height * spriteAspect;
        return new Rect(rect.center.x - width * 0.5f, rect.yMin, width, rect.height);
    }

    private static Rect BoundsOf(Vector3[] points)
    {
        float minX = points[0].x, maxX = points[0].x, minY = points[0].y, maxY = points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, points[i].x);
            maxX = Mathf.Max(maxX, points[i].x);
            minY = Mathf.Min(minY, points[i].y);
            maxY = Mathf.Max(maxY, points[i].y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void LaunchBanner()
    {
        bannerLaunched = true;
        float side = Random.value < 0.5f ? -1f : 1f;
        bannerVelocity = new Vector2(side * bannerKickSideSpeed, bannerKickUpSpeed);
        bannerAngularVelocity = -side * bannerKickSpin;
        bannerAngle = bannerTransform.localEulerAngles.z;
        bannerSquash = bannerImpactSquash;
        bannerSquashVelocity = 0f;
        hitStopTimer = impactHitStop;

        // Knocked back by the hit: the button snaps down to its pressed size with a
        // hard tilt the opposite way to the banner, then settles.
        scaleTarget = 1f + scalePerPress * startPressCount;
        scaleCurrent = scaleTarget - pressSnapPop;
        scaleVelocity = 0f;
        angleCurrent = side * pressSnapTiltDegrees * 1.4f;
        angleVelocity = 0f;
        SoundManager.Play(Sfx.Push, 0.8f);
    }

    private IEnumerator LoadFirstSceneAfterDelay()
    {
        if (finalPressDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(finalPressDelay);
        }

        // Give the slam time to reach the banner, then let it fly before leaving.
        float timeout = 2f;
        while (!bannerLaunched && timeout > 0f && bannerTransform != null)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (bannerLaunched && bannerFlightTime > 0f)
        {
            yield return new WaitForSecondsRealtime(impactHitStop + bannerFlightTime);
        }

        RoomTransitionOverlay.LoadRoom(firstSceneName);
    }

    // ---- Pixel button art ------------------------------------------------------

    private static readonly string[] GlyphS =
    {
        ".####",
        "#....",
        "#....",
        ".###.",
        "....#",
        "....#",
        "####.",
    };

    private static readonly string[] GlyphT =
    {
        "#####",
        "..#..",
        "..#..",
        "..#..",
        "..#..",
        "..#..",
        "..#..",
    };

    private static readonly string[] GlyphA =
    {
        ".###.",
        "#...#",
        "#...#",
        "#####",
        "#...#",
        "#...#",
        "#...#",
    };

    private static readonly string[] GlyphR =
    {
        "####.",
        "#...#",
        "#...#",
        "####.",
        "#.#..",
        "#..#.",
        "#...#",
    };

    private const int ButtonArtWidth = 41;
    private const int ButtonArtHeight = 16;

    private void ApplyPixelButtonStyle()
    {
        Image image = startButtonTransform.GetComponent<Image>();
        Button button = startButtonTransform.GetComponent<Button>();
        if (image == null)
        {
            return;
        }

        Sprite normal = CreateButtonSprite(new Color32(242, 196, 84, 255), new Color32(255, 232, 150, 255), false);
        Sprite highlighted = CreateButtonSprite(new Color32(255, 214, 110, 255), new Color32(255, 244, 190, 255), false);
        Sprite pressed = CreateButtonSprite(new Color32(226, 170, 64, 255), new Color32(246, 210, 120, 255), true);

        image.sprite = normal;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        startButtonTransform.sizeDelta = new Vector2(
            ButtonArtWidth * buttonPixelScale,
            ButtonArtHeight * buttonPixelScale);

        if (button != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = highlighted,
                selectedSprite = normal,
                pressedSprite = pressed,
                disabledSprite = pressed,
            };
        }

        // The label is drawn into the pixel art, so hide the smooth legacy text.
        Text[] labels = startButtonTransform.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].enabled = false;
        }
    }

    private static Sprite CreateButtonSprite(Color32 fill, Color32 highlight, bool pressed)
    {
        int width = ButtonArtWidth;
        int height = ButtonArtHeight;
        var pixels = new Color32[width * height];
        var clear = new Color32(0, 0, 0, 0);
        var outline = new Color32(43, 26, 18, 255);
        var lip = new Color32(178, 104, 40, 255);
        var textColor = new Color32(59, 32, 20, 255);
        var textShadow = new Color32(255, 240, 190, 255);

        // Pressed: the face sinks two pixels onto the lip.
        int lipRows = pressed ? 1 : 3;
        int top = pressed ? height - 3 : height - 1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 color = clear;
                if (y <= top)
                {
                    bool edgeX = x == 0 || x == width - 1;
                    bool edgeY = y == 0 || y == top;
                    bool corner = edgeX && edgeY;
                    if (corner)
                    {
                        color = clear;
                    }
                    else if (edgeX || edgeY)
                    {
                        color = outline;
                    }
                    else if (y <= lipRows)
                    {
                        color = lip;
                    }
                    else if (y == top - 1)
                    {
                        color = highlight;
                    }
                    else
                    {
                        color = fill;
                    }
                }

                pixels[y * width + x] = color;
            }
        }

        // Inner corner pixels round the frame off a little more.
        SetPixel(pixels, width, height, 1, 1, outline);
        SetPixel(pixels, width, height, width - 2, 1, outline);
        SetPixel(pixels, width, height, 1, top - 1, outline);
        SetPixel(pixels, width, height, width - 2, top - 1, outline);

        string[][] glyphs = { GlyphS, GlyphT, GlyphA, GlyphR, GlyphT };
        int textWidth = glyphs.Length * 5 + (glyphs.Length - 1);
        int textLeft = (width - textWidth) / 2;
        int faceBottom = lipRows + 1;
        int faceTop = top - 1;
        int textBottom = faceBottom + (faceTop - faceBottom + 1 - 7) / 2 + 1;
        for (int g = 0; g < glyphs.Length; g++)
        {
            int glyphLeft = textLeft + g * 6;
            for (int row = 0; row < 7; row++)
            {
                string line = glyphs[g][row];
                int y = textBottom + 6 - row;
                for (int column = 0; column < 5; column++)
                {
                    if (line[column] != '#')
                    {
                        continue;
                    }

                    SetPixel(pixels, width, height, glyphLeft + column + 1, y - 1, textShadow, onlyOver: fill);
                    SetPixel(pixels, width, height, glyphLeft + column, y, textColor);
                }
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Pixel Start Button",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 1f);
    }

    private static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            pixels[y * width + x] = color;
        }
    }

    private static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color, Color32 onlyOver)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        Color32 current = pixels[y * width + x];
        if (current.r == onlyOver.r && current.g == onlyOver.g && current.b == onlyOver.b && current.a == onlyOver.a)
        {
            pixels[y * width + x] = color;
        }
    }
}
