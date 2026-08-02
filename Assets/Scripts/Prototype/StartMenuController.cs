using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private int startPressCount;
    private bool isStarting;
    private Vector3 startButtonInitialScale = Vector3.one;

    private void Awake()
    {
        if (startButtonTransform != null)
        {
            startButtonInitialScale = startButtonTransform.localScale;
        }
    }

    private void Update()
    {
        if (bannerImage != null && bannerFrames != null && bannerFrames.Length > 0)
        {
            int frameIndex = Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % bannerFrames.Length;
            bannerImage.sprite = bannerFrames[frameIndex];
        }

        if (bannerTransform != null)
        {
            Vector2 position = bannerTransform.anchoredPosition;
            position.y = 90f + Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmount;
            bannerTransform.anchoredPosition = position;
        }
    }

    public void StartGame()
    {
        if (isStarting)
        {
            return;
        }

        startPressCount++;
        if (startButtonTransform != null)
        {
            float scale = 1f + scalePerPress * startPressCount;
            startButtonTransform.localScale = startButtonInitialScale * scale;
        }

        if (startPressCount >= pressesToStart)
        {
            isStarting = true;
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

    private IEnumerator LoadFirstSceneAfterDelay()
    {
        if (finalPressDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(finalPressDelay);
        }

        SceneManager.LoadScene(firstSceneName);
    }
}
