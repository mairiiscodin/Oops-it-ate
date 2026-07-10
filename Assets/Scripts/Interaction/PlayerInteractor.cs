using OopsItAte.Actors;
using OopsItAte.Grid;
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

        public void Initialize(GridMover playerMover, PlayerInventory playerInventory, KitchenStation kitchenStation, PetBody petBody)
        {
            player = playerMover;
            inventory = playerInventory;
            kitchen = kitchenStation;
            pet = petBody;
        }

        public void TryInteract()
        {
            if (player.CurrentPosition.Equals(kitchen.Position))
            {
                inventory.TryTakeFood();
                return;
            }

            if (inventory.HasFood && pet.IsAdjacentTo(player.CurrentPosition))
            {
                GridPosition playerPositionBeforeGrowth = player.CurrentPosition;
                List<GridPosition> growthCells;
                GridPosition pushDirection;
                bool shouldPushPlayer;
                List<PushableBoxMove> boxMoves;

                if (!pet.TryFindGrowthPlan(
                    playerPositionBeforeGrowth,
                    player.CanMoveTo,
                    out growthCells,
                    out pushDirection,
                    out shouldPushPlayer,
                    out boxMoves))
                {
                    pet.BurpAndShrink();
                    inventory.TryUseFood();
                    return;
                }

                if (!TryApplyBoxMoves(boxMoves))
                {
                    return;
                }

                if (pet.TryGrow(growthCells))
                {
                    if (shouldPushPlayer)
                    {
                        player.TryMove(pushDirection);
                    }

                    inventory.TryUseFood();
                }
                else
                {
                    RollbackBoxMoves(boxMoves);
                }
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
