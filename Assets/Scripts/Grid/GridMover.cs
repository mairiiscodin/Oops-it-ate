using UnityEngine;

namespace OopsItAte.Grid
{
    public sealed class GridMover : MonoBehaviour
    {
        [SerializeField] private GridWorld world;
        [SerializeField] private GridPosition currentPosition = new GridPosition(1, 1);
        [SerializeField] private GridPosition facingDirection = new GridPosition(0, -1);

        private Transform facingIndicator;

        public GridPosition CurrentPosition => currentPosition;
        public GridWorld World => world;
        public GridPosition FacingDirection => facingDirection;
        public GridPosition FacingPosition => currentPosition + facingDirection;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            world = gridWorld;
            currentPosition = startPosition;
            EnsureFacingIndicator();
            SnapToGrid();
            RefreshFacingIndicator();
        }

        public bool TryMove(GridPosition direction)
        {
            if (!direction.Equals(default))
            {
                facingDirection = direction;
                RefreshFacingIndicator();
            }

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
