using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Levels;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Interaction
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private GridMover player;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private KitchenStation kitchen;
        [SerializeField] private PetBody pet;
        [SerializeField] private PushableBox[] boxes;

        public void Initialize(
            GridMover playerMover,
            PlayerInventory playerInventory,
            KitchenStation kitchenStation,
            PetBody petBody,
            PushableBox[] boxes = null)
        {
            player = playerMover;
            inventory = playerInventory;
            kitchen = kitchenStation;
            pet = petBody;
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
            if (pet != null && pet.Contains(targetPosition))
            {
                return pet;
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
            GridPosition playerPositionBeforeGrowth = player.CurrentPosition;
            if (!targetBody.TryFindGrowthPlan(
                playerPositionBeforeGrowth,
                player.CanMoveTo,
                out List<GridPosition> growthCells,
                out GridPosition pushDirection,
                out bool shouldPushPlayer,
                out List<PushableBoxMove> boxMoves,
                out List<PetBodyMove> bodyMoves))
            {
                targetBody.BurpAndShrink();
                inventory.TryUseFood();
                return;
            }

            if (!TryApplyBoxMoves(boxMoves))
            {
                return;
            }

            if (!TryApplyBodyMoves(bodyMoves))
            {
                RollbackBoxMoves(boxMoves);
                return;
            }

            if (targetBody.TryGrow(growthCells))
            {
                if (shouldPushPlayer)
                {
                    player.MoveTo(player.CurrentPosition + pushDirection);
                }

                inventory.TryUseFood();
            }
            else
            {
                RollbackBodyMoves(bodyMoves);
                RollbackBoxMoves(boxMoves);
            }
        }

        private static bool TryApplyBodyMoves(IReadOnlyList<PetBodyMove> bodyMoves)
        {
            int movedCount = 0;
            for (int i = 0; i < bodyMoves.Count; i++)
            {
                if (bodyMoves[i].Body.TryShift(bodyMoves[i].Direction))
                {
                    movedCount++;
                    continue;
                }

                RollbackBodyMoves(bodyMoves, movedCount);
                return false;
            }

            return true;
        }

        private static void RollbackBodyMoves(IReadOnlyList<PetBodyMove> bodyMoves)
        {
            RollbackBodyMoves(bodyMoves, bodyMoves.Count);
        }

        private static void RollbackBodyMoves(IReadOnlyList<PetBodyMove> bodyMoves, int movedCount)
        {
            for (int i = movedCount - 1; i >= 0; i--)
            {
                GridPosition direction = bodyMoves[i].Direction;
                bodyMoves[i].Body.TryShift(new GridPosition(-direction.X, -direction.Y));
            }
        }

        private static bool TryApplyBoxMoves(IReadOnlyList<PushableBoxMove> boxMoves)
        {
            int movedCount = 0;
            for (int i = 0; i < boxMoves.Count; i++)
            {
                if (boxMoves[i].Box.TryMove(boxMoves[i].Direction))
                {
                    movedCount++;
                    continue;
                }

                RollbackBoxMoves(boxMoves, movedCount);
                return false;
            }

            return true;
        }

        private static void RollbackBoxMoves(IReadOnlyList<PushableBoxMove> boxMoves)
        {
            RollbackBoxMoves(boxMoves, boxMoves.Count);
        }

        private static void RollbackBoxMoves(IReadOnlyList<PushableBoxMove> boxMoves, int movedCount)
        {
            for (int i = movedCount - 1; i >= 0; i--)
            {
                GridPosition direction = boxMoves[i].Direction;
                boxMoves[i].Box.TryMove(new GridPosition(-direction.X, -direction.Y));
            }
        }
    }
}
