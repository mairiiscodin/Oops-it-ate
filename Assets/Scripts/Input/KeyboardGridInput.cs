using OopsItAte.Grid;
using OopsItAte.Interaction;
using OopsItAte.Levels;
using UnityEngine;

namespace OopsItAte.Input
{
    public sealed class KeyboardGridInput : MonoBehaviour
    {
        [SerializeField] private GridMover mover;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private LevelExitController exitController;

        public void Initialize(GridMover gridMover, PlayerInteractor playerInteractor, LevelExitController levelExitController)
        {
            mover = gridMover;
            interactor = playerInteractor;
            exitController = levelExitController;
        }

        public void Initialize(GridMover gridMover, PlayerInteractor playerInteractor)
        {
            Initialize(gridMover, playerInteractor, null);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W))
            {
                TryMove(new GridPosition(0, 1));
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                TryMove(new GridPosition(0, -1));
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                TryMove(new GridPosition(-1, 0));
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                TryMove(new GridPosition(1, 0));
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.J))
            {
                interactor.TryInteract();
            }
        }

        private void TryMove(GridPosition direction)
        {
            if (mover.TryMove(direction))
            {
                if (exitController != null)
                {
                    exitController.CheckExit(mover.CurrentPosition);
                }
            }
        }
    }
}
