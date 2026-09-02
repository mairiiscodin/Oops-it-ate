using System.Collections;
using OopsItAte.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OopsItAte.Levels
{
    public sealed class RoomTransitionOverlay : MonoBehaviour
    {
        private const string IrisShaderResource = "IrisTransition";
        private const string SilhouetteResource = "IrisSilhouette";

        // Change this single value to adjust both scene changes and restarts.
        private const float TransitionDuration = 0.5f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int OpenRadiusId = Shader.PropertyToID("_OpenRadius");
        private static readonly int SilhouetteId = Shader.PropertyToID("_Silhouette");
        private static readonly int SilhouetteUvRectId = Shader.PropertyToID("_SilhouetteUvRect");
        private static readonly int SilhouetteAspectId = Shader.PropertyToID("_SilhouetteAspect");
        private static readonly int SilhouetteOpenScaleId = Shader.PropertyToID("_SilhouetteOpenScale");
        private static readonly int UseSilhouetteId = Shader.PropertyToID("_UseSilhouette");

        private static RoomTransitionOverlay instance;
        private CanvasGroup canvasGroup;
        private Material irisMaterial;
        private bool isTransitioning;

        public static void LoadRoom(string sceneName)
        {
            EnsureInstance();
            instance.BeginTransition(() => SceneManager.LoadSceneAsync(sceneName));
        }

        public static void LoadRoom(int buildIndex)
        {
            EnsureInstance();
            instance.BeginTransition(() => SceneManager.LoadSceneAsync(buildIndex));
        }

        public static void Play(System.Action onCovered, System.Action onComplete = null)
        {
            EnsureInstance();
            instance.BeginTransition(onCovered, onComplete);
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

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
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            var imageObject = new GameObject("Iris");
            imageObject.transform.SetParent(transform, false);
            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.AddComponent<Image>();
            image.color = Color.black;

            Shader shader = Resources.Load<Shader>(IrisShaderResource)
                ?? Shader.Find("OopsItAte/UI/IrisTransition");
            if (shader == null)
            {
                Debug.LogWarning("Iris transition shader was not found. Falling back to fade.", this);
                canvasGroup.alpha = 0f;
                return;
            }

            irisMaterial = new Material(shader) { name = "Iris Transition Material" };
            Sprite[] silhouetteSprites = Resources.LoadAll<Sprite>(SilhouetteResource);
            if (silhouetteSprites.Length > 0)
            {
                ConfigureSilhouette(silhouetteSprites[0]);
            }
            else
            {
                Texture2D silhouetteTexture = Resources.Load<Texture2D>(SilhouetteResource);
                if (silhouetteTexture != null)
                {
                    ConfigureSilhouette(
                        silhouetteTexture,
                        new Rect(0f, 0f, silhouetteTexture.width, silhouetteTexture.height));
                }
            }

            image.material = irisMaterial;
            irisMaterial.SetFloat(OpenRadiusId, GetOpenRadius());
            SetIrisProgress(1f);
        }

        private void ConfigureSilhouette(Sprite silhouette)
        {
            ConfigureSilhouette(silhouette.texture, silhouette.textureRect);
        }

        private void ConfigureSilhouette(Texture2D texture, Rect textureRect)
        {
            float inverseWidth = 1f / texture.width;
            float inverseHeight = 1f / texture.height;
            irisMaterial.SetTexture(SilhouetteId, texture);
            irisMaterial.SetVector(
                SilhouetteUvRectId,
                new Vector4(
                    textureRect.x * inverseWidth,
                    textureRect.y * inverseHeight,
                    textureRect.width * inverseWidth,
                    textureRect.height * inverseHeight));
            irisMaterial.SetFloat(
                SilhouetteAspectId,
                textureRect.width / Mathf.Max(1f, textureRect.height));
            irisMaterial.SetFloat(
                SilhouetteOpenScaleId,
                GetSilhouetteOpenScale(textureRect));
            irisMaterial.SetFloat(UseSilhouetteId, 1f);
        }

        private static float GetSilhouetteOpenScale(Rect textureRect)
        {
            float screenAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            float silhouetteAspect = textureRect.width / Mathf.Max(1f, textureRect.height);
            float scaleNeededForScreen = Mathf.Max(1f, screenAspect / silhouetteAspect);
            return scaleNeededForScreen * 1.45f;
        }

        private void BeginTransition(System.Func<AsyncOperation> loadScene)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(LoadRoomRoutine(loadScene));
        }

        private void BeginTransition(System.Action onCovered, System.Action onComplete)
        {
            if (isTransitioning) return;

            isTransitioning = true;
            StartCoroutine(PlayRoutine(onCovered, onComplete));
        }

        private IEnumerator PlayRoutine(System.Action onCovered, System.Action onComplete)
        {
            if (irisMaterial != null)
                yield return AnimateIris(1f, 0f, TransitionDuration);
            else
                yield return FadeTo(1f, TransitionDuration);

            onCovered?.Invoke();
            yield return null;

            if (irisMaterial != null)
                yield return AnimateIris(0f, 1f, TransitionDuration);
            else
                yield return FadeTo(0f, TransitionDuration);

            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator LoadRoomRoutine(System.Func<AsyncOperation> loadScene)
        {
            KeyboardGridInput input = FindAnyObjectByType<KeyboardGridInput>();
            if (input != null) input.enabled = false;

            AsyncOperation operation = loadScene();
            if (operation != null)
            {
                operation.allowSceneActivation = false;
            }

            if (irisMaterial != null)
                yield return AnimateIris(1f, 0f, TransitionDuration);
            else
                yield return FadeTo(1f, TransitionDuration);

            if (operation != null)
            {
                while (operation.progress < 0.9f) yield return null;
                operation.allowSceneActivation = true;
                while (!operation.isDone) yield return null;
            }

            yield return null;

            if (irisMaterial != null)
                yield return AnimateIris(0f, 1f, TransitionDuration);
            else
                yield return FadeTo(0f, TransitionDuration);

            Destroy(gameObject);
        }

        private IEnumerator AnimateIris(float startProgress, float targetProgress, float duration)
        {
            if (duration <= 0f)
            {
                SetIrisProgress(targetProgress);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                SetIrisProgress(Mathf.Lerp(startProgress, targetProgress, progress));
                yield return null;
            }

            SetIrisProgress(targetProgress);
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

        private void SetIrisProgress(float progress)
        {
            irisMaterial.SetFloat(ProgressId, progress);
        }

        private static float GetOpenRadius()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            return Mathf.Sqrt(0.25f * aspect * aspect + 0.25f) + 0.05f;
        }

        private void OnDestroy()
        {
            if (irisMaterial != null) Destroy(irisMaterial);
            if (instance == this) instance = null;
        }
    }
}
