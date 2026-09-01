using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Levels;
using UnityEngine;

namespace OopsItAte.Interaction
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private KitchenStation kitchen;
        [SerializeField] private PetBody[] pets;
        [SerializeField] private PushableBox[] boxes;

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            KitchenStation kitchenStation,
            PetBody[] petBodies,
            PushableBox[] boxes = null)
        {
            player = playerMover;
            inventory = playerInventory;
            kitchen = kitchenStation;
            pets = petBodies ?? new PetBody[0];
            this.boxes = boxes ?? new PushableBox[0];
        }

        public void TryInteract()
        {
            GridPosition targetPosition = player.FacingPosition;

            if (kitchen != null
                && kitchen.GrowableBody != null
                && kitchen.GrowableBody.Contains(targetPosition)
                && !inventory.HasFood)
            {
                inventory.TryTakeFood();
                return;
            }

            PetBody targetBody = inventory.HasFood ? FindBodyAt(targetPosition) : null;
            if (inventory.HasFood && targetBody != null)
            {
                TryFeed(targetBody);
                return;
            }

            if (inventory.HasFood && TryFeedDoorAt(targetPosition))
            {
                inventory.TryUseFood();
                return;
            }

            if (inventory.HasFood
                && player.World.TryPushBorder(targetPosition, player.FacingDirection))
            {
                inventory.TryUseFood();
                return;
            }

            if (kitchen != null && targetPosition.Equals(kitchen.Position))
            {
                if (inventory.HasFood)
                {
                    TryFeed(kitchen.GrowableBody);
                }
                else
                {
                    inventory.TryTakeFood();
                }

                return;
            }

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

            if (kitchen != null && kitchen.GrowableBody != null
                && kitchen.GrowableBody.Contains(targetPosition))
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

        private void TryFeed(PetBody targetBody)
        {
            if (!BodyGrowthPlanner.TryBuild(targetBody, player, out BodyGrowthPlan plan))
            {
                return;
            }

            if (plan.TryCommit(targetBody) && inventory.TryUseFood())
            {
                targetBody.MarkFed();
            }
        }
    }
}
