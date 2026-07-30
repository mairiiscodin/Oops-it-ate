using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroCutsceneController : MonoBehaviour
{
    [SerializeField] private Image cutsceneImage;
    [SerializeField] private Sprite[] cutsceneOneFrames;
    [SerializeField] private Sprite[] cutsceneTwoFrames;
    [SerializeField] private float framesPerSecond = 8f;
    [SerializeField] private string gameplaySceneName = "1";

    private int frameIndex;
    private float timer;
    private bool isPlayingSecondCutscene;

    private void Awake()
    {
        ShowCurrentFrame();
    }

    private void Update()
    {
        Sprite[] currentFrames = GetCurrentFrames();
        if (currentFrames == null || currentFrames.Length == 0)
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        if (!isPlayingSecondCutscene && WasAdvancePressed())
        {
            isPlayingSecondCutscene = true;
            frameIndex = 0;
            timer = 0f;
            ShowCurrentFrame();
            return;
        }

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
            if (isPlayingSecondCutscene)
            {
                SceneManager.LoadScene(gameplaySceneName);
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

    private Sprite[] GetCurrentFrames()
    {
        if (isPlayingSecondCutscene)
        {
            return cutsceneTwoFrames;
        }

        return cutsceneOneFrames;
    }

    private static bool WasAdvancePressed()
    {
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
    }
}
