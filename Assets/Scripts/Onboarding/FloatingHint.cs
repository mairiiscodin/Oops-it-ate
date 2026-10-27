using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>World-space hint sprite that pops in/out and bobs above an anchor point.</summary>
    public sealed class FloatingHint : MonoBehaviour
    {
        public const int SortingOrder = 30000;

        private const float FadeSpeed = 7f;
        private const float BobHeight = 0.06f;
        private const float BobSpeed = 4f;

        private SpriteRenderer spriteRenderer;
        private float baseScale = 1f;
        private float visibility;
        private bool shown;
        private Vector3 anchor;
        private float bobOffset;

        public static FloatingHint Create(string hintName, Sprite sprite, Transform parent, float worldHeight)
        {
            var hintObject = new GameObject(hintName);
            hintObject.transform.SetParent(parent, false);

            var hint = hintObject.AddComponent<FloatingHint>();
            hint.spriteRenderer = hintObject.AddComponent<SpriteRenderer>();
            hint.spriteRenderer.sprite = sprite;
            hint.spriteRenderer.sortingOrder = SortingOrder;
            hint.baseScale = sprite != null
                ? worldHeight / Mathf.Max(sprite.bounds.size.y, 0.0001f)
                : 1f;
            hint.bobOffset = Random.value * Mathf.PI * 2f;
            hint.ApplyVisibility();
            return hint;
        }

        public void SetShown(bool value) => shown = value;

        /// <summary>Bottom-center of the hint sprite is placed at this world point.</summary>
        public void SetAnchor(Vector3 worldPosition) => anchor = worldPosition;

        public void HideImmediately()
        {
            shown = false;
            visibility = 0f;
            ApplyVisibility();
        }

        private void LateUpdate()
        {
            visibility = Mathf.MoveTowards(visibility, shown ? 1f : 0f, Time.unscaledDeltaTime * FadeSpeed);
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            spriteRenderer.enabled = visibility > 0f;
            if (!spriteRenderer.enabled)
            {
                return;
            }

            // Ease-out-back so the hint overshoots a little when it appears.
            float t = visibility - 1f;
            float pop = 1f + 2.2f * t * t * t + 1.2f * t * t;
            float scale = baseScale * Mathf.Max(0f, pop);
            transform.localScale = new Vector3(scale, scale, 1f);

            float halfHeight = spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.extents.y * scale
                : 0f;
            float bob = Mathf.Sin(Time.unscaledTime * BobSpeed + bobOffset) * BobHeight;
            transform.position = anchor + new Vector3(0f, halfHeight + bob, -0.5f);

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(visibility * 1.5f);
            spriteRenderer.color = color;
        }
    }
}
