using UnityEngine;
using UnityEngine.UI;

namespace OopsItAte.Grid
{
    /// <summary>
    /// Renders the camera at 1:1 art pixels into a small texture (so tiles never show
    /// seams), then draws that texture over the whole screen with a sharp-upscale
    /// shader. The result fills the window at any size, stays crisp, and has no
    /// uneven-pixel stripes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelArtUpscaler : MonoBehaviour
    {
        private const string ShaderResource = "SharpPixelUpscale";
        private const string ShaderName = "OopsItAte/UI/SharpPixelUpscale";

        private Camera sourceCamera;
        private RenderTexture renderTexture;
        private GameObject outputRoot;
        private Camera outputCamera;
        private RawImage outputImage;
        private Material outputMaterial;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public int ReferenceWidth { get; private set; }
        public int ReferenceHeight { get; private set; }
        public int PixelsPerUnit { get; private set; }

        public static PixelArtUpscaler GetOrAdd(Camera camera)
        {
            PixelArtUpscaler upscaler = camera.GetComponent<PixelArtUpscaler>();
            return upscaler != null ? upscaler : camera.gameObject.AddComponent<PixelArtUpscaler>();
        }

        /// <summary>
        /// Sets the art-pixel resolution the camera renders at. Both sizes should be
        /// even so the texture edges sit on the art pixel grid.
        /// </summary>
        public bool Configure(int width, int height, int pixelsPerUnit)
        {
            if (!EnsureOutput())
            {
                return false;
            }

            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
            ReferenceWidth = width;
            ReferenceHeight = height;
            PixelsPerUnit = pixelsPerUnit;

            if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
            {
                ReleaseTexture();
                // URP's render graph requires a depth buffer on a camera's output texture.
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Pixel Art Render Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                renderTexture.Create();
                outputImage.texture = renderTexture;
            }

            sourceCamera.targetTexture = renderTexture;
            sourceCamera.orthographicSize = height * 0.5f / pixelsPerUnit;
            SnapCameraToPixelGrid();
            outputCamera.backgroundColor = sourceCamera.backgroundColor;
            LayoutOutput();
            return true;
        }

        private void LateUpdate()
        {
            if (outputImage == null)
            {
                return;
            }

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                LayoutOutput();
            }

            outputCamera.backgroundColor = sourceCamera.backgroundColor;
        }

        private void OnDisable()
        {
            if (sourceCamera != null && sourceCamera.targetTexture == renderTexture)
            {
                sourceCamera.targetTexture = null;
            }

            if (outputRoot != null)
            {
                outputRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (outputRoot != null)
            {
                outputRoot.SetActive(true);
                if (sourceCamera != null && renderTexture != null)
                {
                    sourceCamera.targetTexture = renderTexture;
                }
            }
        }

        private void OnDestroy()
        {
            if (sourceCamera != null && sourceCamera.targetTexture == renderTexture)
            {
                sourceCamera.targetTexture = null;
            }

            ReleaseTexture();
            if (outputMaterial != null)
            {
                Destroy(outputMaterial);
            }

            if (outputRoot != null)
            {
                Destroy(outputRoot);
            }
        }

        private bool EnsureOutput()
        {
            if (outputImage != null)
            {
                return true;
            }

            sourceCamera = GetComponent<Camera>();
            Shader shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }

            if (shader == null)
            {
                Debug.LogWarning("Sharp pixel upscale shader was not found.", this);
                return false;
            }

            outputMaterial = new Material(shader) { name = "Sharp Pixel Upscale Material" };

            // Not parented to the camera: the camera may be moved or zoomed, while
            // this output stays fixed on screen.
            outputRoot = new GameObject("Pixel Art Screen Output");

            // Something has to clear the back buffer now that the main camera draws
            // into a texture; this camera renders nothing else.
            outputCamera = outputRoot.AddComponent<Camera>();
            outputCamera.cullingMask = 0;
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = sourceCamera.backgroundColor;
            outputCamera.orthographic = true;
            outputCamera.depth = sourceCamera.depth + 1f;

            var canvas = outputRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MinValue;

            var imageObject = new GameObject("Pixel Art Image", typeof(RectTransform));
            imageObject.transform.SetParent(outputRoot.transform, false);
            outputImage = imageObject.AddComponent<RawImage>();
            outputImage.material = outputMaterial;
            outputImage.raycastTarget = false;
            return true;
        }

        // Fits the texture inside the screen with one uniform (usually fractional)
        // scale, centred. The reference size already matches the screen aspect, so
        // this fills the window apart from at most a pixel or two.
        private void LayoutOutput()
        {
            if (outputImage == null || ReferenceWidth <= 0 || ReferenceHeight <= 0)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            float scale = Mathf.Min(
                (float)Screen.width / ReferenceWidth,
                (float)Screen.height / ReferenceHeight);

            RectTransform rect = outputImage.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ReferenceWidth * scale, ReferenceHeight * scale);
        }

        // With an even texture size and the camera centre on an art pixel corner,
        // every texel lines up with exactly one art pixel.
        private void SnapCameraToPixelGrid()
        {
            if (PixelsPerUnit <= 0)
            {
                return;
            }

            Vector3 position = sourceCamera.transform.position;
            position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
            position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
            sourceCamera.transform.position = position;
        }

        private void ReleaseTexture()
        {
            if (renderTexture == null)
            {
                return;
            }

            if (outputImage != null && outputImage.texture == renderTexture)
            {
                outputImage.texture = null;
            }

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }
}
