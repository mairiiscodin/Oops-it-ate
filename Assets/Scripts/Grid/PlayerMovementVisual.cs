using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OopsItAte.Grid
{
    public sealed class PlayerMovementVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite frontSprite;
        [SerializeField] private Sprite backSprite;
        [SerializeField] private Sprite sideSprite;
        [SerializeField] private int sortingOrderBase = 1000;
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("Step")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.14f;
        [SerializeField, Min(0f)] private float hopHeight = 0.12f;
        [SerializeField, Range(0f, 0.25f)] private float airStretch = 0.06f;
        [SerializeField, Range(0f, 0.25f)] private float landingSquash = 0.09f;

        [Header("Blocked step")]
        [SerializeField, Min(0.01f)] private float bumpDuration = 0.09f;
        [SerializeField, Min(0f)] private float bumpDistance = 0.08f;

        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Coroutine animationRoutine;

        public bool IsAnimating => animationRoutine != null;

        private void Awake()
        {
            EnsureReferences();
            CaptureBasePose();
            SetFacing(new GridPosition(0, -1));
        }

        public void SetFacing(GridPosition direction)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            UpdateSortingOrder();

            if (direction.Y > 0)
            {
                if (backSprite != null) spriteRenderer.sprite = backSprite;
                spriteRenderer.flipX = false;
            }
            else if (direction.Y < 0)
            {
                if (frontSprite != null) spriteRenderer.sprite = frontSprite;
                spriteRenderer.flipX = false;
            }
            else if (direction.X != 0)
            {
                if (sideSprite != null) spriteRenderer.sprite = sideSprite;
                spriteRenderer.flipX = direction.X < 0;
            }
        }

        private void UpdateSortingOrder()
        {
            spriteRenderer.sortingOrder = sortingOrderBase
                + Mathf.RoundToInt(-transform.position.y * 100f)
                + sortingOrderOffset;
        }

        public void PlayStep(Vector3 worldDelta, GridPosition direction)
        {
            SetFacing(direction);
            RestartAnimation(AnimateStep(worldDelta));
        }

        public void PlayBlockedStep(GridPosition direction, float cellSize)
        {
            SetFacing(direction);
            Vector3 worldDirection = new Vector3(direction.X, direction.Y, 0f).normalized;
            RestartAnimation(AnimateBump(worldDirection, Mathf.Min(bumpDistance, cellSize * 0.2f)));
        }

        private IEnumerator AnimateStep(Vector3 worldDelta)
        {
            Vector3 localDelta = visual.parent != null
                ? visual.parent.InverseTransformVector(worldDelta)
                : worldDelta;
            float elapsed = 0f;

            while (elapsed < stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stepDuration);
                float eased = 1f - (1f - t) * (1f - t);
                float airborne = Mathf.Sin(t * Mathf.PI);
                float landing = t > 0.72f
                    ? Mathf.Sin((t - 0.72f) / 0.28f * Mathf.PI)
                    : 0f;

                visual.localPosition = baseLocalPosition
                    + Vector3.Lerp(-localDelta, Vector3.zero, eased)
                    + Vector3.up * (airborne * hopHeight);

                float xScale = 1f - airborne * airStretch + landing * landingSquash;
                float yScale = 1f + airborne * airStretch - landing * landingSquash;
                visual.localScale = Vector3.Scale(baseLocalScale, new Vector3(xScale, yScale, 1f));
                yield return null;
            }

            FinishAnimation();
        }

        private IEnumerator AnimateBump(Vector3 worldDirection, float distance)
        {
            Vector3 localDirection = visual.parent != null
                ? visual.parent.InverseTransformVector(worldDirection)
                : worldDirection;
            float elapsed = 0f;

            while (elapsed < bumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bumpDuration);
                float amount = Mathf.Sin(t * Mathf.PI) * distance;
                float squash = Mathf.Sin(t * Mathf.PI) * landingSquash;
                visual.localPosition = baseLocalPosition + localDirection * amount;
                visual.localScale = Vector3.Scale(
                    baseLocalScale,
                    new Vector3(1f + squash, 1f - squash, 1f));
                yield return null;
            }

            FinishAnimation();
        }

        private void RestartAnimation(IEnumerator animation)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            ResetPose();
            animationRoutine = StartCoroutine(animation);
        }

        private void FinishAnimation()
        {
            ResetPose();
            animationRoutine = null;
        }

        private void CaptureBasePose()
        {
            if (visual == null)
            {
                return;
            }

            baseLocalPosition = visual.localPosition;
            baseLocalScale = visual.localScale;
        }

        private void ResetPose()
        {
            if (visual == null)
            {
                return;
            }

            visual.localPosition = baseLocalPosition;
            visual.localScale = baseLocalScale;
        }

        private void OnDisable()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            ResetPose();
        }

        private void OnValidate()
        {
            EnsureReferences();
            if (!Application.isPlaying && spriteRenderer != null && frontSprite != null)
            {
                spriteRenderer.sprite = frontSprite;
                spriteRenderer.flipX = false;
            }
        }

        private void EnsureReferences()
        {
            if (visual == null)
            {
                Transform candidate = transform.Find("PlayerVisual");
                visual = candidate != null ? candidate : transform;
            }

            if (spriteRenderer == null && visual != null)
            {
                spriteRenderer = visual.GetComponent<SpriteRenderer>();
            }

#if UNITY_EDITOR
            if (frontSprite == null) frontSprite = LoadSprite("Assets/Assets/ChefFront.aseprite");
            if (backSprite == null) backSprite = LoadSprite("Assets/Assets/ChefBack.aseprite");
            if (sideSprite == null) sideSprite = LoadSprite("Assets/Assets/ChefSide.aseprite");
#endif
        }

#if UNITY_EDITOR
        private static Sprite LoadSprite(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    return sprite;
                }
            }

            return null;
        }
#endif
    }
}
