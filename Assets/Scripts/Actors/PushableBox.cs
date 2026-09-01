using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PushableBox : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.62f, 0.36f, 0.16f);
        [SerializeField] private GridPosition position;
        [SerializeField] private PetBody growableBody;

        private GridWorld world;
        private bool isBlocking;

        public GridPosition Position => position;
        public bool IsInitialized => world != null;
        public bool IsPushable => world != null && growableBody == null;
        public PetBody GrowableBody => growableBody;
        internal GridWorld World => world;

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
            return world != null && world.CanTraverse(position, targetPosition);
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

        internal void SetPositionUnchecked(GridPosition targetPosition)
        {
            RemoveBlocker();
            position = targetPosition;
            SnapToGrid();
            AddBlocker();
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
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }

            SpriteRenderer authoredVisual = GetComponentInChildren<SpriteRenderer>(true);
            if (authoredVisual != null)
            {
                authoredVisual.enabled = true;
            }
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

        private void OnDestroy()
        {
            RemoveBlocker();
        }

        private void OnValidate()
        {
            MeshRenderer placeholder = GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
        }

    }
}
