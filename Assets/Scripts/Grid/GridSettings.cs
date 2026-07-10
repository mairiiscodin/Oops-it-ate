using System;
using UnityEngine;

namespace OopsItAte.Grid
{
    [Serializable]
    public sealed class GridSettings
    {
        [Min(1)] public int width = 7;
        [Min(1)] public int height = 7;
        [Min(0.1f)] public float cellSize = 1f;

        public Vector3 GridToWorld(GridPosition position)
        {
            float xOffset = (width - 1) * cellSize * 0.5f;
            float yOffset = (height - 1) * cellSize * 0.5f;
            return new Vector3(position.X * cellSize - xOffset, position.Y * cellSize - yOffset, 0f);
        }

        public bool IsInside(GridPosition position)
        {
            return position.X >= 0 && position.X < width && position.Y >= 0 && position.Y < height;
        }

        public GridPosition WorldToGrid(Vector3 worldPosition)
        {
            float xOffset = (width - 1) * cellSize * 0.5f;
            float yOffset = (height - 1) * cellSize * 0.5f;
            int x = Mathf.RoundToInt((worldPosition.x + xOffset) / cellSize);
            int y = Mathf.RoundToInt((worldPosition.y + yOffset) / cellSize);
            return new GridPosition(x, y);
        }
    }
}
