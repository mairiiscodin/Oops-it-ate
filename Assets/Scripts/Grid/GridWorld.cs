using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Grid
{
    public sealed class GridWorld : MonoBehaviour
    {
        [SerializeField] private GridSettings settings;
        [SerializeField] private Color floorColor = new Color(0.16f, 0.16f, 0.16f);
        [SerializeField] private Color wallColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly HashSet<GridPosition> blockedCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> dynamicBlockedCells = new HashSet<GridPosition>();

        public GridSettings Settings => settings;

        public void Initialize(GridSettings gridSettings, IEnumerable<GridPosition> levelWalls)
        {
            settings = gridSettings;
            BuildWalls(levelWalls);
            DrawGrid();
        }

        public bool CanEnter(GridPosition position)
        {
            return settings.IsInside(position) && !blockedCells.Contains(position) && !dynamicBlockedCells.Contains(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return !settings.IsInside(position) || blockedCells.Contains(position) || dynamicBlockedCells.Contains(position);
        }

        public void AddDynamicBlocker(GridPosition position)
        {
            dynamicBlockedCells.Add(position);
        }

        public void RemoveDynamicBlocker(GridPosition position)
        {
            dynamicBlockedCells.Remove(position);
        }

        private void BuildWalls(IEnumerable<GridPosition> levelWalls)
        {
            blockedCells.Clear();

            if (levelWalls == null)
            {
                return;
            }

            foreach (GridPosition wall in levelWalls)
            {
                if (settings.IsInside(wall))
                {
                    blockedCells.Add(wall);
                }
            }
        }

        private void DrawGrid()
        {
            for (int y = 0; y < settings.height; y++)
            {
                for (int x = 0; x < settings.width; x++)
                {
                    var position = new GridPosition(x, y);
                    bool isWall = blockedCells.Contains(position);
                    CreateCell(position, isWall ? wallColor : floorColor, isWall ? "Wall" : "Floor");
                }
            }
        }

        private void CreateCell(GridPosition position, Color color, string label)
        {
            GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cell.name = $"{label} {position}";
            cell.transform.SetParent(transform);
            cell.transform.position = settings.GridToWorld(position);
            cell.transform.localScale = Vector3.one * settings.cellSize * 0.92f;

            var renderer = cell.GetComponent<MeshRenderer>();
            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = color;

            Destroy(cell.GetComponent<Collider>());
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }
    }
}
