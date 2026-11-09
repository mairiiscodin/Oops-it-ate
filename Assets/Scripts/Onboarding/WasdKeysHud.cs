using UnityEngine;
using UnityEngine.UI;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// WASD keycaps on the left edge of the screen. The keys press themselves in turn
    /// (W, A, S, D) and also light up when the player presses the real key.
    /// </summary>
    public sealed class WasdKeysHud : MonoBehaviour
    {
        private const int CanvasSortingOrder = 100;
        private const float KeySize = 64f;
        private const float KeyGap = 6f;
        private const float ScreenMargin = 40f;
        private const float SlideDistance = 60f;
        private const float FadeSpeed = 4f;
        private const float PressStep = 0.32f;
        private const float PressHold = 0.18f;

        private static readonly char[] Letters = { 'W', 'A', 'S', 'D' };
        private static readonly KeyCode[] Keys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
        private static readonly KeyCode[] Arrows = { KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow };

        private readonly Image[] keyImages = new Image[Letters.Length];
        private RectTransform container;
        private CanvasGroup canvasGroup;
        private bool shown;
        private float visibility;
        private float pressTimer;

        public static WasdKeysHud Create(Transform parent)
        {
            var canvasObject = new GameObject("Hint WASD");
            canvasObject.transform.SetParent(parent, false);
            var hud = canvasObject.AddComponent<WasdKeysHud>();
            hud.Build();
            return hud;
        }

        public void SetShown(bool value) => shown = value;

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            float keyHeight = KeySize * 12f / 11f;
            var containerObject = new GameObject("Keys", typeof(RectTransform));
            containerObject.transform.SetParent(transform, false);
            container = (RectTransform)containerObject.transform;
            container.anchorMin = new Vector2(0f, 0.5f);
            container.anchorMax = new Vector2(0f, 0.5f);
            container.pivot = new Vector2(0f, 0.5f);
            container.sizeDelta = new Vector2(KeySize * 3f + KeyGap * 2f, keyHeight * 2f + KeyGap);

            // W sits above S; A, S, D form the bottom row.
            Vector2[] slots =
            {
                new Vector2(1, 1),
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(2, 0),
            };
            for (int i = 0; i < Letters.Length; i++)
            {
                var keyObject = new GameObject($"Key {Letters[i]}", typeof(RectTransform));
                keyObject.transform.SetParent(container, false);
                var rect = (RectTransform)keyObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(KeySize, keyHeight);
                rect.anchoredPosition = new Vector2(slots[i].x * (KeySize + KeyGap), slots[i].y * (keyHeight + KeyGap));

                keyImages[i] = keyObject.AddComponent<Image>();
                keyImages[i].sprite = OnboardingArt.Key(Letters[i], false);
                keyImages[i].preserveAspect = true;
                keyImages[i].raycastTarget = false;
            }

            Apply();
        }

        private void Update()
        {
            visibility = Mathf.MoveTowards(visibility, shown ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
            if (visibility <= 0f && !shown)
            {
                pressTimer = 0f;
                Apply();
                return;
            }

            pressTimer += Time.unscaledDeltaTime;
            int demoKey = Mathf.FloorToInt(pressTimer / PressStep) % Letters.Length;
            bool demoPressed = pressTimer % PressStep < PressHold;
            for (int i = 0; i < Letters.Length; i++)
            {
                bool pressed = UnityEngine.Input.GetKey(Keys[i])
                    || UnityEngine.Input.GetKey(Arrows[i])
                    || (demoPressed && demoKey == i);
                keyImages[i].sprite = OnboardingArt.Key(Letters[i], pressed);
            }

            Apply();
        }

        private void Apply()
        {
            canvasGroup.alpha = visibility;
            float eased = 1f - (1f - visibility) * (1f - visibility);
            container.anchoredPosition = new Vector2(ScreenMargin - SlideDistance * (1f - eased), 0f);
        }
    }
}
