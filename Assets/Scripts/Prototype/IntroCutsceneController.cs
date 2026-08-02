using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroCutsceneController : MonoBehaviour
{
    [SerializeField] private Image cutsceneImage;
    [SerializeField] private Sprite[] cutsceneOneFrames;
    [SerializeField] private Sprite[] cutsceneTwoFrames;
    [SerializeField] private Sprite[] cutsceneThreeFrames;
    [SerializeField] private float framesPerSecond = 8f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    [SerializeField] private string gameplaySceneName = "1";

    private int frameIndex;
    private float timer;
    private float fadeTimer;
    private bool isFading;
    private CutscenePhase phase;

    private enum CutscenePhase
    {
        FirstLoop,
        SecondOnce,
        ThirdLoop
    }

    private void Awake()
    {
        ShowCurrentFrame();
        BeginFadeIn();
    }

    private void Update()
    {
        Sprite[] currentFrames = GetCurrentFrames();
        if (currentFrames == null || currentFrames.Length == 0)
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        if (phase == CutscenePhase.FirstLoop && WasAdvancePressed())
        {
            StartPhase(CutscenePhase.SecondOnce);
            return;
        }

        if (phase == CutscenePhase.ThirdLoop && WasAdvancePressed())
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        UpdateFade();

        timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, framesPerSecond);

        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        Sprite[] currentFrames = GetCurrentFrames();
        frameIndex++;
        if (frameIndex >= currentFrames.Length)
        {
            if (phase == CutscenePhase.SecondOnce)
            {
                StartPhase(CutscenePhase.ThirdLoop);
                return;
            }

            frameIndex = 0;
        }
 
        ShowCurrentFrame();
    }

    private void ShowCurrentFrame()
    {
        Sprite[] currentFrames = GetCurrentFrames();
        if (cutsceneImage != null && currentFrames != null && currentFrames.Length > 0)
        {
            cutsceneImage.sprite = currentFrames[frameIndex];
        }
    }

    private void BeginFadeIn()
    {
        fadeTimer = 0f;
        isFading = fadeDuration > 0f;
        SetCutsceneAlpha(isFading ? 0f : 1f);
    }

    private void UpdateFade()
    {
        if (!isFading)
        {
            return;
        }

        fadeTimer += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(fadeTimer / fadeDuration);
        SetCutsceneAlpha(Mathf.SmoothStep(0f, 1f, progress));

        if (progress >= 1f)
        {
            isFading = false;
        }
    }

    private void SetCutsceneAlpha(float alpha)
    {
        if (cutsceneImage == null)
        {
            return;
        }

        Color color = cutsceneImage.color;
        color.a = alpha;
        cutsceneImage.color = color;
    }

    private void StartPhase(CutscenePhase nextPhase)
    {
        phase = nextPhase;
        frameIndex = 0;
        timer = 0f;
        ShowCurrentFrame();
        BeginFadeIn();
    }

    private Sprite[] GetCurrentFrames()
    {
        switch (phase)
        {
            case CutscenePhase.SecondOnce:
                return cutsceneTwoFrames;
            case CutscenePhase.ThirdLoop:
                return cutsceneThreeFrames;
            default:
                return cutsceneOneFrames;
        }
    }

    private static bool WasAdvancePressed()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
    }
}
