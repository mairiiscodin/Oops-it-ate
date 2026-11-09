using OopsItAte.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OopsItAte.Levels
{
    /// <summary>
    /// Gear button in the top-right corner that opens a sound panel with Music and SFX
    /// sliders. Esc also toggles it. Gameplay keys are ignored while the panel is open.
    /// </summary>
    public sealed class SettingsHud : MonoBehaviour
    {
        // Above the mini map, below RoomTransitionOverlay.
        private const int CanvasSortingOrder = 110;
        private const float ScreenMargin = 24f;
        private const float ButtonSize = 44f;
        private const float PanelWidth = 260f;
        private const float PanelHeight = 132f;

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color OpenPanelColor = new Color(0.07f, 0.07f, 0.11f, 0.9f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color FillColor = new Color32(255, 222, 116, 255);
        private static readonly Color TextColor = new Color32(244, 244, 236, 255);

        private GameObject panel;
        private Font font;

        /// <summary>True while the sound panel is open; KeyboardGridInput ignores keys then.</summary>
        public static bool IsOpen { get; private set; }

        public void Initialize()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildCanvas();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            IsOpen = false;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                SetOpen(!IsOpen);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (panel != null)
            {
                panel.SetActive(open);
            }

            // Drop UI focus so WASD/arrows never drive the button or sliders.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Settings");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Image buttonImage = CreateImage("Settings Button", canvasObject.transform, PanelColor);
            RectTransform buttonRect = buttonImage.rectTransform;
            AnchorTopRight(buttonRect, new Vector2(ButtonSize, ButtonSize), new Vector2(-ScreenMargin, -ScreenMargin));
            var button = buttonImage.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SetOpen(!IsOpen));

            Image gear = CreateImage("Gear", buttonRect, Color.white);
            gear.sprite = BuildGearSprite();
            gear.raycastTarget = false;
            Stretch(gear.rectTransform, 8f);

            Image panelImage = CreateImage("Sound Panel", canvasObject.transform, OpenPanelColor);
            panel = panelImage.gameObject;
            AnchorTopRight(
                panelImage.rectTransform,
                new Vector2(PanelWidth, PanelHeight),
                new Vector2(-ScreenMargin, -ScreenMargin - ButtonSize - 8f));

            CreateText("Title", panelImage.rectTransform, "SOUND", 20, new Vector2(16f, -12f), new Vector2(200f, 26f));
            CreateVolumeRow(panelImage.rectTransform, "MUSIC", -52f, SoundManager.MusicVolume, value => SoundManager.MusicVolume = value);
            CreateVolumeRow(panelImage.rectTransform, "SFX", -92f, SoundManager.SfxVolume, value =>
            {
                SoundManager.SfxVolume = value;
                SoundManager.Play(Sfx.Step);
            });
        }

        private void CreateVolumeRow(RectTransform parent, string label, float top, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            CreateText($"{label} Label", parent, label, 16, new Vector2(16f, top), new Vector2(70f, 24f));

            var sliderObject = new GameObject($"{label} Slider", typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);
            var sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.anchorMin = Vector2.up;
            sliderRect.anchorMax = Vector2.up;
            sliderRect.pivot = Vector2.up;
            sliderRect.sizeDelta = new Vector2(150f, 24f);
            sliderRect.anchoredPosition = new Vector2(92f, top);

            Image track = CreateImage("Track", sliderRect, TrackColor);
            Stretch(track.rectTransform, 0f);
            track.rectTransform.offsetMin = new Vector2(0f, 8f);
            track.rectTransform.offsetMax = new Vector2(0f, -8f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderRect, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            Stretch(fillAreaRect, 0f);
            fillAreaRect.offsetMin = new Vector2(0f, 8f);
            fillAreaRect.offsetMax = new Vector2(0f, -8f);
            Image fill = CreateImage("Fill", fillAreaRect, FillColor);
            Stretch(fill.rectTransform, 0f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderRect, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            Stretch(handleAreaRect, 0f);
            handleAreaRect.offsetMin = new Vector2(6f, 0f);
            handleAreaRect.offsetMax = new Vector2(-6f, 0f);
            Image handle = CreateImage("Handle", handleAreaRect, TextColor);
            handle.rectTransform.sizeDelta = new Vector2(12f, 0f);

            var slider = sliderObject.AddComponent<Slider>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(onChanged);
        }

        private Text CreateText(string name, RectTransform parent, string content, int size, Vector2 topLeft, Vector2 boxSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.sizeDelta = boxSize;
            rect.anchoredPosition = topLeft;

            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>16x16 pixel gear: eight teeth around a ring with a hole in the middle.</summary>
        private static Sprite BuildGearSprite()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            Color32 gearColor = new Color32(244, 244, 236, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 7.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    bool tooth = Mathf.Cos(Mathf.Atan2(dy, dx) * 8f) > 0.25f;
                    float outer = tooth ? 7.6f : 5.6f;
                    bool inside = radius <= outer && radius >= 2.3f;
                    pixels[y * size + x] = inside ? gearColor : new Color32(0, 0, 0, 0);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
