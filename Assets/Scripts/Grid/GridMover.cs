using UnityEngine;

namespace OopsItAte.Grid
{
    public sealed class GridMover : MonoBehaviour
    {
        [SerializeField] private GridWorld world;
        [SerializeField] private GridPosition currentPosition = new GridPosition(1, 1);
        [SerializeField] private GridPosition facingDirection = new GridPosition(0, -1);
        [SerializeField] private PlayerMovementVisual movementVisual;

        private Transform facingIndicator;

        public GridPosition CurrentPosition => currentPosition;
        public GridWorld World => world;
        public GridPosition FacingDirection => facingDirection;
        public GridPosition FacingPosition => currentPosition + facingDirection;
        public bool IsMoving => movementVisual != null && movementVisual.IsAnimating;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            world = gridWorld;
            currentPosition = startPosition;
            EnsureMovementVisual();
            EnsureFacingIndicator();
            SnapToGrid();
            RefreshFacingIndicator();
            movementVisual?.SetFacing(facingDirection);
        }

        public bool TryMove(GridPosition direction)
        {
            if (IsMoving)
            {
                return false;
            }

            if (!direction.Equals(default))
            {
                facingDirection = direction;
                RefreshFacingIndicator();
                movementVisual?.SetFacing(facingDirection);
            }

            GridPosition nextPosition = currentPosition + direction;

            if (!CanMoveTo(nextPosition))
            {
                movementVisual?.PlayBlockedStep(direction, world.Settings.cellSize);
                return false;
            }

            Vector3 previousWorldPosition = world.Settings.GridToWorld(currentPosition);
            currentPosition = nextPosition;
            SnapToGrid();
            Vector3 worldDelta = world.Settings.GridToWorld(currentPosition) - previousWorldPosition;
            movementVisual?.PlayStep(worldDelta, direction);
            return true;
        }

        public bool CanMoveTo(GridPosition position)
        {
            return world.CanEnter(position);
        }

        public void MoveTo(GridPosition position)
        {
            GridPosition direction = new GridPosition(
                System.Math.Sign(position.X - currentPosition.X),
                System.Math.Sign(position.Y - currentPosition.Y));
            Vector3 previousWorldPosition = world.Settings.GridToWorld(currentPosition);
            currentPosition = position;
            SnapToGrid();

            if (!direction.Equals(default))
            {
                facingDirection = direction;
                RefreshFacingIndicator();
                Vector3 worldDelta = world.Settings.GridToWorld(currentPosition) - previousWorldPosition;
                movementVisual?.PlayStep(worldDelta, direction);
            }
        }

        private void EnsureMovementVisual()
        {
            if (movementVisual == null)
            {
                movementVisual = GetComponent<PlayerMovementVisual>();
            }
        }

        private void SnapToGrid()
        {
            transform.position = world.Settings.GridToWorld(currentPosition) + Vector3.back;
            world.SetPlayerPosition(currentPosition);
        }

        private void EnsureFacingIndicator()
        {
            if (facingIndicator != null)
            {
                return;
            }

            var indicator = new GameObject("Facing Indicator");
            indicator.transform.SetParent(transform, false);

            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.18f, -0.12f, 0f),
                new Vector3(0.18f, -0.12f, 0f),
                new Vector3(0f, 0.22f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            indicator.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = indicator.AddComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = new Color(0.02f, 0.12f, 0.2f);
            facingIndicator = indicator.transform;
        }

        private void RefreshFacingIndicator()
        {
            if (facingIndicator == null)
            {
                return;
            }

            facingIndicator.localPosition = new Vector3(
                facingDirection.X * 0.42f,
                facingDirection.Y * 0.42f,
                -0.1f);
            facingIndicator.localRotation = Quaternion.Euler(0f, 0f,
                -Mathf.Atan2(facingDirection.X, facingDirection.Y) * Mathf.Rad2Deg);
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }
    }
}
