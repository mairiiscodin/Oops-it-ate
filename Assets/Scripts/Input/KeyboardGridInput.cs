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
        private GridPosition? queuedDirection;

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
            GridPosition? pressedDirection = ReadMovementDirection();

            if (mover.IsMoving)
            {
                if (pressedDirection.HasValue)
                {
                    queuedDirection = pressedDirection;
                }

                return;
            }

            if (queuedDirection.HasValue)
            {
                GridPosition direction = queuedDirection.Value;
                queuedDirection = null;
                TryMove(direction);
                return;
            }

            if (pressedDirection.HasValue)
            {
                TryMove(pressedDirection.Value);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.J))
            {
                interactor.TryInteract();
            }
        }

        private static GridPosition? ReadMovementDirection()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W))
            {
                return new GridPosition(0, 1);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                return new GridPosition(0, -1);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                return new GridPosition(-1, 0);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                return new GridPosition(1, 0);
            }

            return null;
        }

        private void TryMove(GridPosition direction)
        {
            if (mover.TryMove(direction))
            {
                if (exitController != null)
                {
                    exitController.CheckExit(mover.CurrentPosition);
                }

                return;
            }

            if (exitController != null)
            {
                exitController.CheckExit(mover.FacingPosition);
            }
        }
    }
}
