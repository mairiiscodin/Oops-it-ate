using System;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Grid
{
    internal sealed class GridCellView
    {
        private const float CellScale = 1f;

        private readonly Transform parent;
        private readonly GridSettings settings;
        private readonly Color floorColor;
        private readonly Color wallColor;
        private readonly Dictionary<GridPosition, MeshRenderer> renderers =
            new Dictionary<GridPosition, MeshRenderer>();

        public GridCellView(Transform parent, GridSettings settings, Color floorColor, Color wallColor)
        {
            this.parent = parent;
            this.settings = settings;
            this.floorColor = floorColor;
            this.wallColor = wallColor;
        }

        public void Draw(IEnumerable<GridPosition> cells, Func<GridPosition, bool> isWall)
        {
            Clear();
            foreach (GridPosition position in cells)
            {
                SetCell(position, isWall(position));
            }
        }

        public void SetCell(GridPosition position, bool isWall)
        {
            if (!renderers.TryGetValue(position, out MeshRenderer renderer))
            {
                renderer = CreateCell(position);
                renderers.Add(position, renderer);
            }

            renderer.material.color = isWall ? wallColor : floorColor;
            renderer.gameObject.name = $"{(isWall ? "Wall" : "Floor")} {position}";
        }

        public void RemoveCell(GridPosition position)
        {
            if (!renderers.TryGetValue(position, out MeshRenderer renderer))
            {
                return;
            }

            UnityEngine.Object.Destroy(renderer.gameObject);
            renderers.Remove(position);
        }

        private MeshRenderer CreateCell(GridPosition position)
        {
            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cell.transform.SetParent(parent);
            cell.transform.position = settings.GridToWorld(position);
            cell.transform.localScale = Vector3.one * settings.cellSize * CellScale;

            var renderer = cell.GetComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            UnityEngine.Object.Destroy(cell.GetComponent<Collider>());
            return renderer;
        }

        private void Clear()
        {
            foreach (MeshRenderer renderer in renderers.Values)
            {
                UnityEngine.Object.Destroy(renderer.gameObject);
            }

            renderers.Clear();
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }
    }
}
