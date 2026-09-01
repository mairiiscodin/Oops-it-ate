using OopsItAte.Actors;
using OopsItAte.Grid;
using OopsItAte.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Levels
{
    public sealed class LevelExitController : MonoBehaviour
    {
        [SerializeField] private DoorExit[] doors;
        [SerializeField] private string roomId;
        private bool isLoadingScene;

        public void Initialize(DoorExit[] sceneDoors, GridWorld gridWorld, string currentRoomId)
        {
            doors = sceneDoors;
            roomId = string.IsNullOrWhiteSpace(currentRoomId)
                ? SceneManager.GetActiveScene().name
                : currentRoomId.Trim();
            GameSession.EnterRoom(roomId);

            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].Initialize(gridWorld);
            }
        }

        public bool TryConsumeArrivalPosition(out GridPosition arrivalPosition)
        {
            if (!GameSession.TryConsumeArrival(
                out string sourceRoomId,
                out string sourceSceneName,
                out string targetDoorId))
            {
                arrivalPosition = default;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(targetDoorId))
            {
                for (int i = 0; i < doors.Length; i++)
                {
                    DoorExit targetDoor = doors[i];
                    if (string.Equals(targetDoor.DoorId, targetDoorId,
                            System.StringComparison.OrdinalIgnoreCase)
                        && targetDoor.TryGetInteriorPosition(out arrivalPosition))
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < doors.Length; i++)
            {
                DoorExit door = doors[i];
                if ((string.Equals(door.TargetSceneName, sourceSceneName,
                         System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(door.TargetSceneName, sourceRoomId,
                        System.StringComparison.OrdinalIgnoreCase)
                    )
                    && door.TryGetInteriorPosition(out arrivalPosition))
                {
                    return true;
                }
            }

            Debug.LogWarning(
                $"No arrival door '{targetDoorId}' from room '{sourceRoomId}' was found. Using PlayerStart instead.");
            arrivalPosition = default;
            return false;
        }

        public void CheckExit(GridPosition playerPosition, GridPosition movementDirection)
        {
            if (isLoadingScene)
            {
                return;
            }

            for (int i = 0; i < doors.Length; i++)
            {
                DoorExit door = doors[i];
                if (!door.CanOpenFrom(playerPosition, movementDirection))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(door.TargetSceneName))
                {
                    Debug.LogWarning($"Door {door.name} has no target scene.");
                    return;
                }

                if (!Application.CanStreamedLevelBeLoaded(door.TargetSceneName))
                {
                    Debug.LogError(
                        $"Door {door.name} cannot load scene '{door.TargetSceneName}'. "
                        + "Add that scene to Build Profiles > Scene List.");
                    return;
                }

                PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
                if (door.IsLocked)
                {
                    if (!door.RequiresFood || inventory == null || !inventory.HasFood)
                    {
                        Debug.Log($"Door '{door.DoorId}' is locked.", door);
                        return;
                    }

                    inventory.TryUseFood();
                    GameSession.UnlockDoor(roomId, door.DoorId);
                }

                isLoadingScene = true;
                SaveRoomState(inventory);
                GameSession.BeginRoomTransition(
                    roomId,
                    SceneManager.GetActiveScene().name,
                    door.TargetDoorId,
                    inventory != null && inventory.HasFood);
                RoomTransitionOverlay.LoadRoom(door.TargetSceneName);
                return;
            }
        }

        private void SaveRoomState(PlayerInventory inventory)
        {
            GameSession.SetHasFood(inventory != null && inventory.HasFood);

            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] != null && boxes[i].IsInitialized)
                {
                    GameSession.SaveObjectPosition(
                        roomId,
                        $"Box:{boxes[i].name}",
                        boxes[i].Position);
                }
            }

            PetBody[] pets = FindObjectsByType<PetBody>();
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null && pets[i].GetComponent<KitchenStation>() == null)
                {
                    GameSession.SaveObjectPosition(
                        roomId,
                        $"Pet:{pets[i].name}",
                        pets[i].Origin);
                }
            }
        }
    }
}
