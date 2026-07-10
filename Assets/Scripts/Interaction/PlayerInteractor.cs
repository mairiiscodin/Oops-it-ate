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

                if (!pet.TryFindGrowthPlan(
                    playerPositionBeforeGrowth,
                    player.CanMoveTo,
                    out growthCells,
                    out pushDirection,
                    out shouldPushPlayer))
                {
                    pet.BurpAndShrink();
                    inventory.TryUseFood();
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
            }
        }
    }
}
