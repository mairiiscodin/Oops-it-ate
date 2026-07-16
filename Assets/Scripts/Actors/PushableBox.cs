using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Actors
{
    public readonly struct PushableBoxMove
    {
        public PushableBoxMove(PushableBox box, GridPosition direction)
        {
            Box = box;
            Direction = direction;
        }

        public PushableBox Box { get; }
        public GridPosition Direction { get; }
    }

    public sealed class PushableBox : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.62f, 0.36f, 0.16f);
        [SerializeField] private GridPosition position;
        [SerializeField] private PetBody growableBody;

        private GridWorld world;
        private Material visualMaterial;
        private bool isBlocking;

        public GridPosition Position => position;
        public bool IsInitialized => world != null;
        public bool IsPushable => world != null && growableBody == null;
        public PetBody GrowableBody => growableBody;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            RemoveBlocker();
            world = gridWorld;
            position = startPosition;
            EnsureVisual();
            SnapToGrid();
            AddBlocker();
        }

        public bool CanMoveTo(GridPosition targetPosition)
        {
            return world != null && world.CanEnter(targetPosition);
        }

        public bool TryMove(GridPosition direction)
        {
            GridPosition targetPosition = position + direction;
            if (!CanMoveTo(targetPosition))
            {
                return false;
            }

            RemoveBlocker();
            position = targetPosition;
            SnapToGrid();
            AddBlocker();
            return true;
        }

        public PetBody ConvertToGrowable()
        {
            if (growableBody != null)
            {
                return growableBody;
            }

            RemoveBlocker();
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            growableBody = gameObject.AddComponent<PetBody>();
            growableBody.Initialize(world, position, color, "Box");
            return growableBody;
        }

        internal void SuspendBlocker()
        {
            RemoveBlocker();
        }

        internal void RestoreBlocker()
        {
            AddBlocker();
        }

        private void EnsureVisual()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = CreateQuadMesh();
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (visualMaterial == null)
            {
                visualMaterial = new Material(FindUnlitShader());
                visualMaterial.color = color;
            }

            meshRenderer.sharedMaterial = visualMaterial;
        }

        private void SnapToGrid()
        {
            transform.position = world.Settings.GridToWorld(position) + Vector3.back * 0.75f;
            transform.localScale = Vector3.one * world.Settings.cellSize;
        }

        private void AddBlocker()
        {
            if (world == null || isBlocking || growableBody != null)
            {
                return;
            }

            world.AddDynamicBlocker(position);
            isBlocking = true;
        }

        private void RemoveBlocker()
        {
            if (world == null || !isBlocking)
            {
                return;
            }

            world.RemoveDynamicBlocker(position);
            isBlocking = false;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDestroy()
        {
            RemoveBlocker();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawCube(transform.position, Vector3.one);
        }
    }
}
