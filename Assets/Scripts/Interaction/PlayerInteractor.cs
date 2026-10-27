using OopsItAte.Actors;
using OopsItAte.Audio;
using OopsItAte.Grid;
using OopsItAte.Levels;
using UnityEngine;

namespace OopsItAte.Interaction
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        // Lets the chomp start before the growth pop so the two don't mask each other.
        private const float GrowSoundDelay = 0.12f;

        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private KitchenStation[] kitchens;
        [SerializeField] private PetBody[] pets;
        [SerializeField] private PushableBox[] boxes;

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            KitchenStation kitchenStation,
            PetBody[] petBodies,
            PushableBox[] boxes = null)
        {
            Initialize(
                playerMover,
                playerInventory,
                kitchenStation == null ? null : new[] { kitchenStation },
                petBodies,
                boxes);
        }

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            KitchenStation[] kitchenStations,
            PetBody[] petBodies,
            PushableBox[] boxes = null)
        {
            player = playerMover;
            inventory = playerInventory;
            kitchens = kitchenStations ?? new KitchenStation[0];
            pets = petBodies ?? new PetBody[0];
            this.boxes = boxes ?? new PushableBox[0];
        }

        public void TryInteract()
        {
            GridPosition targetPosition = player.FacingPosition;

            KitchenStation targetKitchen = FindKitchenAt(targetPosition);
            if (targetKitchen != null && !inventory.HasFood)
            {
                inventory.TryTakeFood();
                return;
            }

            PetBody targetBody = inventory.HasFood ? FindBodyAt(targetPosition) : null;
            if (inventory.HasFood && targetBody != null)
            {
                if (!TryFeed(targetBody))
                {
                    SoundManager.Play(Sfx.Nope);
                }

                return;
            }

            if (inventory.HasFood && TryFeedDoorAt(targetPosition))
            {
                inventory.TryUseFood();
                SoundManager.Play(Sfx.Eat);
                SoundManager.Play(Sfx.Grow, 0.85f, delay: GrowSoundDelay);
                return;
            }

            if (inventory.HasFood
                && player.World.TryPushBorder(targetPosition, player.FacingDirection))
            {
                inventory.TryUseFood();
                SoundManager.Play(Sfx.Eat);
                SoundManager.Play(Sfx.BorderPush, delay: GrowSoundDelay);
                return;
            }

            if (inventory.HasFood)
            {
                // Holding food but facing nothing that can eat it.
                SoundManager.Play(Sfx.Nope);
            }

        }

        private KitchenStation FindKitchenAt(GridPosition targetPosition)
        {
            for (int i = 0; i < kitchens.Length; i++)
            {
                KitchenStation kitchen = kitchens[i];
                if (kitchen == null)
                {
                    continue;
                }

                if ((kitchen.GrowableBody != null
                        && kitchen.GrowableBody.Contains(targetPosition))
                    || targetPosition.Equals(kitchen.Position))
                {
                    return kitchen;
                }
            }

            return null;
        }

        private static bool TryFeedDoorAt(GridPosition targetPosition)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].Contains(targetPosition))
                {
                    return doors[i].FeedAndGrow();
                }
            }

            return false;
        }

        private PetBody FindBodyAt(GridPosition targetPosition)
        {
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null && pets[i].Contains(targetPosition))
                {
                    return pets[i];
                }
            }

            KitchenStation kitchen = FindKitchenAt(targetPosition);
            if (kitchen != null && kitchen.GrowableBody != null)
            {
                return kitchen.GrowableBody;
            }

            for (int i = 0; i < boxes.Length; i++)
            {
                PushableBox box = boxes[i];
                if (box == null || !box.IsInitialized)
                {
                    continue;
                }

                if (box.GrowableBody != null && box.GrowableBody.Contains(targetPosition))
                {
                    return box.GrowableBody;
                }

                if (box.GrowableBody == null && targetPosition.Equals(box.Position))
                {
                    return box.ConvertToGrowable();
                }
            }

            return null;
        }

        private bool TryFeed(PetBody targetBody)
        {
            if (!BodyGrowthPlanner.TryBuild(targetBody, player, out BodyGrowthPlan plan))
            {
                return false;
            }

            if (!plan.TryCommit(targetBody) || !inventory.TryUseFood())
            {
                return false;
            }

            targetBody.MarkFed();
            SoundManager.Play(Sfx.Eat);
            // Bigger bodies sound deeper with every extra layer.
            SoundManager.Play(Sfx.Grow, Mathf.Max(0.6f, 1.1f - targetBody.GrowthLevel * 0.1f), delay: GrowSoundDelay);
            if (plan.PushCount > 0)
            {
                SoundManager.Play(Sfx.Push, delay: GrowSoundDelay);
            }

            return true;
        }
    }
}
