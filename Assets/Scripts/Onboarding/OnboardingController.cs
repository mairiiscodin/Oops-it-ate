using System.Collections;
using System.Collections.Generic;
using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Input;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// First-room tutorial:
    /// 0. The camera zooms in on the hungry Pet while its bread thought bubble pops up.
    /// 1. WASD keys press themselves on the left edge of the screen.
    /// 2. They disappear once the player has walked a few cells.
    /// 3. The camera zooms in on the oven, which starts smoking until food is picked up.
    /// 4. "J TAKE FOOD" (see InteractionPrompts) is held back until the oven is introduced.
    /// The room's hidden door appears after the completion animation (see LevelExitController).
    /// </summary>
    public sealed class OnboardingController : MonoBehaviour
    {
        private const int CellsToLearnMovement = 3;
        private const float BubbleHeight = 0.62f;
        private const float HintGap = 0.08f;

        private const float FocusZoom = 0.5f;
        private const float ZoomInDuration = 0.7f;
        private const float ZoomOutDuration = 0.55f;
        private const float PetFocusHold = 2f;
        private const float OvenFocusHold = 1.3f;

        private const float SmokeInterval = 0.32f;
        private const float SmokeLifetime = 1.3f;
        private const float SmokeRise = 0.75f;
        private const float SmokeSize = 0.22f;

        // Scenes whose camera intro already played this run, so revisits skip the zooms.
        private static readonly HashSet<string> introducedScenes = new HashSet<string>();

        private sealed class SmokePuff
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public float Age;
            public float Drift;
        }

        private GridMover player;
        private PlayerInventory inventory;
        private KeyboardGridInput gameplayInput;
        private InteractionPrompts prompts;
        private KitchenStation[] kitchens;
        private PetBody[] pets;

        private WasdKeysHud moveHud;
        private readonly Dictionary<PetBody, FloatingHint> petBubbles = new Dictionary<PetBody, FloatingHint>();
        private readonly List<SmokePuff> smokePuffs = new List<SmokePuff>();

        private GridPosition lastPlayerPosition;
        private int cellsWalked;
        private bool hasTakenFood;
        private bool showBubbles;
        private bool isSmoking;
        private bool isFocusing;
        private bool isFinished;
        private float smokeTimer;

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            KeyboardGridInput input,
            InteractionPrompts interactionPrompts,
            KitchenStation[] roomKitchens,
            PetBody[] roomPets)
        {
            player = playerMover;
            inventory = playerInventory;
            gameplayInput = input;
            prompts = interactionPrompts;
            kitchens = roomKitchens ?? new KitchenStation[0];
            pets = roomPets ?? new PetBody[0];
            lastPlayerPosition = player.CurrentPosition;

            if (AllPetsFed())
            {
                // Coming back to a finished room: nothing left to teach.
                isFinished = true;
                return;
            }

            // Food carried in from elsewhere means the oven step is already understood.
            hasTakenFood = inventory.HasFood;
            inventory.HasFoodChanged += HandleHasFoodChanged;

            moveHud = WasdKeysHud.Create(transform);
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null)
                {
                    petBubbles[pets[i]] = FloatingHint.Create(
                        $"Hungry Bubble {pets[i].name}",
                        OnboardingArt.ThoughtBubble,
                        transform,
                        BubbleHeight);
                }
            }

            StartCoroutine(PlayTutorial());
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.HasFoodChanged -= HandleHasFoodChanged;
            }
        }

        private void HandleHasFoodChanged(bool hasFood)
        {
            if (hasFood)
            {
                hasTakenFood = true;
            }
        }

        private IEnumerator PlayTutorial()
        {
            bool playIntro = introducedScenes.Add(gameObject.scene.name);

            // 0. Show the hungry Pet and what it wants.
            if (playIntro)
            {
                SetGameplayInputEnabled(false);
                while (RoomTransitionOverlay.IsPlaying)
                {
                    yield return null;
                }

                PetBody hungryPet = FindHungryPet();
                if (hungryPet != null)
                {
                    yield return FocusCamera(
                        hungryPet,
                        Vector3.up * BubbleHeight * 0.5f,
                        PetFocusHold,
                        () => showBubbles = true);
                }

                SetGameplayInputEnabled(true);
            }

            showBubbles = true;

            // 1-2. WASD until the player has walked a few cells.
            moveHud.SetShown(true);
            while (cellsWalked < CellsToLearnMovement && !hasTakenFood)
            {
                TrackMovement();
                yield return null;
            }

            moveHud.SetShown(false);
            if (hasTakenFood)
            {
                yield break;
            }

            // 3. Show the oven, which smokes until food is taken.
            KitchenStation oven = FindOven();
            if (playIntro && oven != null)
            {
                SetGameplayInputEnabled(false);
                while (player.IsMoving)
                {
                    yield return null;
                }

                yield return FocusCamera(oven, Vector3.up * SmokeRise * 0.4f, OvenFocusHold, () => isSmoking = true);
                SetGameplayInputEnabled(true);
            }

            isSmoking = true;
        }

        private void Update()
        {
            if (isFinished || player == null)
            {
                return;
            }

            if (AllPetsFed())
            {
                Finish();
                return;
            }

            // 4. "J TAKE FOOD" only once the oven has been introduced.
            if (prompts != null)
            {
                prompts.ShowTakeFood = isSmoking || hasTakenFood;
                prompts.Suppressed = isFocusing;
            }

            foreach (KeyValuePair<PetBody, FloatingHint> pair in petBubbles)
            {
                if (pair.Key == null)
                {
                    pair.Value.SetShown(false);
                    continue;
                }

                pair.Value.SetAnchor(GetTopCenter(pair.Key, pair.Key.transform.position));
                pair.Value.SetShown(showBubbles && !pair.Key.HasBeenFed);
            }

            UpdateSmoke(isSmoking && !hasTakenFood);
        }

        private void TrackMovement()
        {
            if (player.CurrentPosition.Equals(lastPlayerPosition))
            {
                return;
            }

            lastPlayerPosition = player.CurrentPosition;
            cellsWalked++;
        }

        /// <summary>
        /// Pans and zooms the camera onto the target, holds, then returns to the room view.
        /// onArrived runs when the camera reaches the target.
        /// </summary>
        private IEnumerator FocusCamera(Component target, Vector3 offset, float holdSeconds, System.Action onArrived)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                onArrived?.Invoke();
                yield break;
            }

            isFocusing = true;
            PixelArtUpscaler upscaler = camera.GetComponent<PixelArtUpscaler>();
            bool usePixelPerfect = upscaler != null && upscaler.enabled && upscaler.PixelsPerUnit > 0;
            Vector3 basePosition = camera.transform.position;
            float baseSize = camera.orthographicSize;
            int baseRefX = usePixelPerfect ? upscaler.ReferenceWidth : 0;
            int baseRefY = usePixelPerfect ? upscaler.ReferenceHeight : 0;
            Vector3 focus = GetCenter(target, target.transform.position) + offset;
            Vector3 focusPosition = new Vector3(focus.x, focus.y, basePosition.z);

            void Apply(float t)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                camera.transform.position = Vector3.Lerp(basePosition, focusPosition, eased);
                float zoom = Mathf.Lerp(1f, FocusZoom, eased);
                if (usePixelPerfect)
                {
                    // Zoom by shrinking the 1:1 art-pixel texture (kept even, like
                    // GridWorld does) so the zoomed view stays crisp and seam-free.
                    upscaler.Configure(
                        MakeEven(Mathf.RoundToInt(baseRefX * zoom)),
                        MakeEven(Mathf.RoundToInt(baseRefY * zoom)),
                        upscaler.PixelsPerUnit);
                }
                else
                {
                    camera.orthographicSize = baseSize * zoom;
                }
            }

            for (float elapsed = 0f; elapsed < ZoomInDuration; elapsed += Time.unscaledDeltaTime)
            {
                Apply(elapsed / ZoomInDuration);
                yield return null;
            }

            Apply(1f);
            onArrived?.Invoke();
            yield return new WaitForSecondsRealtime(holdSeconds);

            for (float elapsed = 0f; elapsed < ZoomOutDuration; elapsed += Time.unscaledDeltaTime)
            {
                Apply(1f - elapsed / ZoomOutDuration);
                yield return null;
            }

            camera.transform.position = basePosition;
            if (usePixelPerfect)
            {
                upscaler.Configure(baseRefX, baseRefY, upscaler.PixelsPerUnit);
            }
            else
            {
                camera.orthographicSize = baseSize;
            }
            isFocusing = false;
        }

        private void SetGameplayInputEnabled(bool value)
        {
            if (gameplayInput != null)
            {
                gameplayInput.enabled = value;
            }
        }

        private void Finish()
        {
            isFinished = true;
            StopAllCoroutines();
            if (isFocusing)
            {
                SetGameplayInputEnabled(true);
            }

            if (prompts != null)
            {
                prompts.ShowTakeFood = true;
                prompts.Suppressed = false;
            }

            moveHud.SetShown(false);
            foreach (FloatingHint bubble in petBubbles.Values)
            {
                bubble.HideImmediately();
            }

            for (int i = 0; i < smokePuffs.Count; i++)
            {
                if (smokePuffs[i].Renderer != null) Destroy(smokePuffs[i].Renderer.gameObject);
            }

            smokePuffs.Clear();
        }

        private bool AllPetsFed()
        {
            bool anyPet = false;
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] == null) continue;
                anyPet = true;
                if (!pets[i].HasBeenFed) return false;
            }

            return anyPet;
        }

        private PetBody FindHungryPet()
        {
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null && !pets[i].HasBeenFed) return pets[i];
            }

            return null;
        }

        private KitchenStation FindOven()
        {
            for (int i = 0; i < kitchens.Length; i++)
            {
                if (kitchens[i] != null) return kitchens[i];
            }

            return null;
        }

        private void UpdateSmoke(bool emit)
        {
            if (emit)
            {
                smokeTimer -= Time.deltaTime;
                if (smokeTimer <= 0f)
                {
                    smokeTimer = SmokeInterval;
                    for (int i = 0; i < kitchens.Length; i++)
                    {
                        if (kitchens[i] != null) SpawnSmoke(kitchens[i]);
                    }
                }
            }

            for (int i = smokePuffs.Count - 1; i >= 0; i--)
            {
                SmokePuff puff = smokePuffs[i];
                puff.Age += Time.deltaTime;
                float t = puff.Age / SmokeLifetime;
                if (t >= 1f || puff.Renderer == null)
                {
                    if (puff.Renderer != null) Destroy(puff.Renderer.gameObject);
                    smokePuffs.RemoveAt(i);
                    continue;
                }

                float wobble = Mathf.Sin(puff.Age * 5f + puff.Drift * 10f) * 0.05f;
                puff.Renderer.transform.position = puff.Origin
                    + new Vector3(puff.Drift * t + wobble, SmokeRise * t, -0.5f);
                float scale = Mathf.Lerp(0.6f, 1.4f, t) * SmokeSize
                    / Mathf.Max(puff.Renderer.sprite.bounds.size.y, 0.0001f);
                puff.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
                Color color = puff.Renderer.color;
                color.a = Mathf.Sin(t * Mathf.PI) * 0.85f;
                puff.Renderer.color = color;
            }
        }

        private void SpawnSmoke(KitchenStation kitchen)
        {
            Component source = kitchen.GrowableBody != null ? kitchen.GrowableBody : (Component)kitchen;
            Vector3 top = GetTopCenter(source, kitchen.transform.position + Vector3.up * 0.4f);

            var puffObject = new GameObject("Oven Smoke");
            puffObject.transform.SetParent(transform, false);
            var renderer = puffObject.AddComponent<SpriteRenderer>();
            renderer.sprite = OnboardingArt.Smoke;
            renderer.sortingOrder = FloatingHint.SortingOrder - 1;
            renderer.color = new Color(1f, 1f, 1f, 0f);

            smokePuffs.Add(new SmokePuff
            {
                Renderer = renderer,
                Origin = top + new Vector3(Random.Range(-0.18f, 0.18f), -0.1f, 0f),
                Drift = Random.Range(-0.15f, 0.15f)
            });
        }

        private static int MakeEven(int value) => Mathf.Max(2, value + (value & 1));

        /// <summary>Top-center of every sprite under the target, plus a small gap.</summary>
        private static Vector3 GetTopCenter(Component target, Vector3 fallback)
        {
            return TryGetSpriteBounds(target, out Bounds bounds)
                ? new Vector3(bounds.center.x, bounds.max.y + HintGap, bounds.center.z)
                : fallback;
        }

        private static Vector3 GetCenter(Component target, Vector3 fallback)
        {
            return TryGetSpriteBounds(target, out Bounds bounds) ? bounds.center : fallback;
        }

        private static bool TryGetSpriteBounds(Component target, out Bounds bounds)
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
            bool hasBounds = false;
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled || renderers[i].sprite == null) continue;
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return hasBounds;
        }
    }
}
