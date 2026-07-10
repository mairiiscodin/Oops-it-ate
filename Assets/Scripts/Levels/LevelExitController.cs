using OopsItAte.Grid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OopsItAte.Levels
{
    public sealed class LevelExitController : MonoBehaviour
    {
        [SerializeField] private DoorExit[] doors;

        public void Initialize(DoorExit[] sceneDoors, GridSettings grid)
        {
            doors = sceneDoors;

            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].Initialize(grid);
            }
        }

        public void CheckExit(GridPosition playerPosition)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                DoorExit door = doors[i];
                if (!door.Position.Equals(playerPosition))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(door.TargetSceneName))
                {
                    Debug.LogWarning($"Door {door.name} has no target scene.");
                    return;
                }

                SceneManager.LoadScene(door.TargetSceneName);
                return;
            }
        }
    }
}
