using System.Collections;
using OopsItAte.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OopsItAte.Levels
{
    public sealed class RoomTransitionOverlay : MonoBehaviour
    {
        private static RoomTransitionOverlay instance;
        private CanvasGroup canvasGroup;

        public static void LoadRoom(string sceneName, float fadeDuration = 0.22f)
        {
            EnsureInstance();
            instance.StartCoroutine(instance.LoadRoomRoutine(sceneName, fadeDuration));
        }

        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            var root = new GameObject("Room Transition");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<RoomTransitionOverlay>();
            instance.BuildOverlay();
        }

        private void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            var imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(transform, false);
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageObject.AddComponent<Image>();
            image.color = Color.black;
        }

        private IEnumerator LoadRoomRoutine(string sceneName, float duration)
        {
            KeyboardGridInput input = FindAnyObjectByType<KeyboardGridInput>();
            if (input != null) input.enabled = false;

            yield return FadeTo(1f, duration);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return FadeTo(0f, duration);
            Destroy(gameObject);
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = canvasGroup.alpha;
            if (duration <= 0f)
            {
                canvasGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            canvasGroup.alpha = target;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
