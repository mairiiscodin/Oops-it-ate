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
    [SerializeField] private string firstSceneName = "IntroCutscene";

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
        SceneManager.LoadScene(firstSceneName);
    }
}
