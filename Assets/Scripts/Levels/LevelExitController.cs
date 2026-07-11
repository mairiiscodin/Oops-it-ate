using OopsItAte.Grid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Levels
{
    public sealed class LevelExitController : MonoBehaviour
    {
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
