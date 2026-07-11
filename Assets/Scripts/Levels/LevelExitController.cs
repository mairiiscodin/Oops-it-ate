using OopsItAte.Grid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Levels
{
    public sealed class LevelExitController : MonoBehaviour
    {
        private static string pendingSourceSceneName;

        [SerializeField] private DoorExit[] doors;
        private bool isLoadingScene;

        private GridWorld world;

        public void Initialize(DoorExit[] sceneDoors, GridWorld gridWorld)
        {
            doors = sceneDoors;
            world = gridWorld;

            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].Initialize(world);
            }

            world.BoundaryExpanded += MoveDoorsWithBoundary;
        }

        private void MoveDoorsWithBoundary(GridPosition direction, GridPosition previousBoundaryPosition)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].MoveWithBoundary(direction, previousBoundaryPosition);
            }
        }

        public bool TryConsumeArrivalPosition(out GridPosition arrivalPosition)
        {
            if (string.IsNullOrWhiteSpace(pendingSourceSceneName))
            {
                arrivalPosition = default;
                return false;
            }

            for (int i = 0; i < doors.Length; i++)
            {
                DoorExit door = doors[i];
                if (string.Equals(door.TargetSceneName, pendingSourceSceneName,
                        System.StringComparison.OrdinalIgnoreCase)
                    && door.TryGetInteriorPosition(out arrivalPosition))
                {
                    pendingSourceSceneName = null;
                    return true;
                }
            }

            Debug.LogWarning(
                $"No return door to scene '{pendingSourceSceneName}' was found. Using PlayerStart instead.");
            pendingSourceSceneName = null;
            arrivalPosition = default;
            return false;
        }

        public void CheckExit(GridPosition playerPosition)
        {
            if (isLoadingScene)
            {
                return;
            }

            for (int i = 0; i < doors.Length; i++)
            {
                DoorExit door = doors[i];
                if (!door.Contains(playerPosition))
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

                isLoadingScene = true;
                pendingSourceSceneName = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(door.TargetSceneName);
                return;
            }
        }

        private void OnDestroy()
        {
            if (world != null)
            {
                world.BoundaryExpanded -= MoveDoorsWithBoundary;
            }
        }
    }
}
