using UnityEngine;

namespace OopsItAte.Grid
{
    public sealed class GridMover : MonoBehaviour
    {
        [SerializeField] private GridWorld world;
        [SerializeField] private GridPosition currentPosition = new GridPosition(1, 1);

        public GridPosition CurrentPosition => currentPosition;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            world = gridWorld;
            currentPosition = startPosition;
            SnapToGrid();
        }

        public bool TryMove(GridPosition direction)
        {
            GridPosition nextPosition = currentPosition + direction;

            if (!CanMoveTo(nextPosition))
            {
                return false;
            }

            currentPosition = nextPosition;
            SnapToGrid();
            return true;
        }

        public bool CanMoveTo(GridPosition position)
        {
            return world.CanEnter(position);
        }

        public void MoveTo(GridPosition position)
        {
            currentPosition = position;
            SnapToGrid();
        }

        private void SnapToGrid()
        {
            transform.position = world.Settings.GridToWorld(currentPosition) + Vector3.back;
        }
    }
}
