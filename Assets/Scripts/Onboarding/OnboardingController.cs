using System.Collections.Generic;
using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// First-room tutorial:
    /// 1. WASD above the player until they have walked a few cells.
    /// 2. The oven smokes until food is picked up; J shows when facing the oven empty-handed.
    /// 3. Hungry Pets show a thought bubble; J shows when facing one while holding food.
    /// 4. The room's hidden door appears after the completion animation (see LevelExitController).
    /// </summary>
    public sealed class OnboardingController : MonoBehaviour
    {
        private const int CellsToLearnMovement = 2;
        private const float KeyHintHeight = 0.42f;
        private const float WasdHintHeight = 0.6f;
        private const float BubbleHeight = 0.62f;
        private const float HintGap = 0.08f;

        private const float SmokeInterval = 0.32f;
        private const float SmokeLifetime = 1.3f;
        private const float SmokeRise = 0.75f;
        private const float SmokeSize = 0.22f;

        private sealed class SmokePuff
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public float Age;
            public float Drift;
        }

        private GridMover player;
        private PlayerInventory inventory;
        private KitchenStation[] kitchens;
        private PetBody[] pets;

        private FloatingHint moveHint;
        private FloatingHint interactHint;
        private readonly Dictionary<PetBody, FloatingHint> petBubbles = new Dictionary<PetBody, FloatingHint>();
        private readonly List<SmokePuff> smokePuffs = new List<SmokePuff>();

        private GridPosition lastPlayerPosition;
        private int cellsWalked;
        private bool hasLearnedMovement;
        private bool hasTakenFood;
        private bool isFinished;
        private float smokeTimer;

        public void Initialize(GridMover playerMover, PlayerInventory playerInventory, KitchenStation[] roomKitchens, PetBody[] roomPets)
        {
            player = playerMover;
            inventory = playerInventory;
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

            moveHint = FloatingHint.Create("Hint WASD", OnboardingArt.KeyWasd, transform, WasdHintHeight);
            interactHint = FloatingHint.Create("Hint J", OnboardingArt.KeyJ, transform, KeyHintHeight);
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

            TrackMovement();

            bool facingOven = !inventory.HasFood && IsFacingKitchen();
            bool facingHungryPet = inventory.HasFood && IsFacingHungryPet();
            bool showInteract = (facingOven || facingHungryPet) && !player.IsMoving;

            Vector3 aboveHead = GetTopCenter(player, player.transform.position + Vector3.up * 0.5f);
            moveHint.SetAnchor(aboveHead);
            moveHint.SetShown(!hasLearnedMovement && !showInteract);
            interactHint.SetAnchor(aboveHead);
            interactHint.SetShown(showInteract);

            foreach (KeyValuePair<PetBody, FloatingHint> pair in petBubbles)
            {
                if (pair.Key == null)
                {
                    pair.Value.SetShown(false);
                    continue;
                }

                pair.Value.SetAnchor(GetTopCenter(pair.Key, pair.Key.transform.position));
                pair.Value.SetShown(hasLearnedMovement && !pair.Key.HasBeenFed);
            }

            UpdateSmoke(!hasTakenFood);
        }

        private void TrackMovement()
        {
            if (hasLearnedMovement || player.CurrentPosition.Equals(lastPlayerPosition))
            {
                return;
            }

            lastPlayerPosition = player.CurrentPosition;
            cellsWalked++;
            hasLearnedMovement = cellsWalked >= CellsToLearnMovement;
        }

        private void Finish()
        {
            isFinished = true;
            moveHint.HideImmediately();
            interactHint.HideImmediately();
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

        private bool IsFacingKitchen()
        {
            GridPosition facing = player.FacingPosition;
            for (int i = 0; i < kitchens.Length; i++)
            {
                KitchenStation kitchen = kitchens[i];
                if (kitchen != null
                    && (facing.Equals(kitchen.Position)
                        || (kitchen.GrowableBody != null && kitchen.GrowableBody.Contains(facing))))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFacingHungryPet()
        {
            GridPosition facing = player.FacingPosition;
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null && !pets[i].HasBeenFed && pets[i].Contains(facing))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>Top-center of every sprite under the target, plus a small gap.</summary>
        private static Vector3 GetTopCenter(Component target, Vector3 fallback)
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
            bool hasBounds = false;
            Bounds bounds = default;
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

            return hasBounds
                ? new Vector3(bounds.center.x, bounds.max.y + HintGap, bounds.center.z)
                : fallback;
        }
    }
}
