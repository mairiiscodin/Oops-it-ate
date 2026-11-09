using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// J prompts above the player in every room: "J TAKE FOOD" when facing an oven
    /// empty-handed, "J FEED" when facing anything that can be fed while holding food
    /// (only Pets when FeedPetsOnly is set, as in the first room).
    /// </summary>
    public sealed class InteractionPrompts : MonoBehaviour
    {
        private const float KeyHintHeight = 0.42f;
        private const float HintGap = 0.08f;

        private GridMover player;
        private PlayerInventory inventory;
        private PlayerInteractor interactor;
        private KitchenStation[] kitchens;
        private PetBody[] pets;

        private FloatingHint takeFoodHint;
        private FloatingHint feedHint;

        /// <summary>The first-room tutorial holds the oven prompt back until the oven is introduced.</summary>
        public bool ShowTakeFood { get; set; } = true;

        /// <summary>Limits "J FEED" to Pets instead of every feedable thing.</summary>
        public bool FeedPetsOnly { get; set; }

        /// <summary>Hides both prompts, e.g. while the tutorial camera is zoomed in.</summary>
        public bool Suppressed { get; set; }

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            PlayerInteractor playerInteractor,
            KitchenStation[] roomKitchens,
            PetBody[] roomPets)
        {
            player = playerMover;
            inventory = playerInventory;
            interactor = playerInteractor;
            kitchens = roomKitchens ?? new KitchenStation[0];
            pets = roomPets ?? new PetBody[0];

            takeFoodHint = FloatingHint.Create("Hint J Take Food", OnboardingArt.KeyJTakeFood, transform, KeyHintHeight);
            feedHint = FloatingHint.Create("Hint J Feed", OnboardingArt.KeyJFeed, transform, KeyHintHeight);
        }

        private void Update()
        {
            if (player == null || inventory == null)
            {
                return;
            }

            bool canPrompt = !Suppressed && !player.IsMoving;
            Vector3 aboveHead = GetTopCenter(player, player.transform.position + Vector3.up * 0.5f);
            takeFoodHint.SetAnchor(aboveHead);
            takeFoodHint.SetShown(canPrompt && ShowTakeFood && !inventory.HasFood && IsFacingKitchen());
            feedHint.SetAnchor(aboveHead);
            feedHint.SetShown(canPrompt && inventory.HasFood && CanFeedAhead());
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

        private bool CanFeedAhead()
        {
            return FeedPetsOnly || interactor == null
                ? IsFacingPet()
                : interactor.HasFeedTargetAhead();
        }

        private bool IsFacingPet()
        {
            GridPosition facing = player.FacingPosition;
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null && pets[i].Contains(facing))
                {
                    return true;
                }
            }

            return false;
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
