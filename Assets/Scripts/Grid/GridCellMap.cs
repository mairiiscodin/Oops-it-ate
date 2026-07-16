using System.Collections.Generic;

namespace OopsItAte.Grid
{
    internal sealed class GridCellMap
    {
        private readonly HashSet<GridPosition> loadedCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> authoredWalls = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> dynamicBlockers = new HashSet<GridPosition>();

        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public int MinY { get; private set; }
        public int MaxY { get; private set; }
        public IEnumerable<GridPosition> LoadedCells => loadedCells;

        public void Initialize(
            int width,
            int height,
            IEnumerable<GridPosition> levelWalls,
            IEnumerable<GridPosition> levelCells)
        {
            MinX = 0;
            MaxX = width - 1;
            MinY = 0;
            MaxY = height - 1;

            loadedCells.Clear();
            authoredWalls.Clear();
            dynamicBlockers.Clear();

            BuildLoadedCells(levelCells);
            BuildWalls(levelWalls);
        }

        public bool CanEnter(GridPosition position)
        {
            return IsLoaded(position) && !IsBlocked(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return !IsLoaded(position) || IsAuthoredWall(position) || IsDynamicBlocker(position);
        }

        public bool IsLoaded(GridPosition position) => loadedCells.Contains(position);
        public bool IsAuthoredWall(GridPosition position) => authoredWalls.Contains(position);
        public bool IsDynamicBlocker(GridPosition position) => dynamicBlockers.Contains(position);

        public void AddDynamicBlocker(GridPosition position) => dynamicBlockers.Add(position);
        public void RemoveDynamicBlocker(GridPosition position) => dynamicBlockers.Remove(position);

        public bool LoadCell(GridPosition position)
        {
            return loadedCells.Add(position);
        }

        public void GetLoadedBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            foreach (GridPosition position in loadedCells)
            {
                if (position.X < minX) minX = position.X;
                if (position.X > maxX) maxX = position.X;
                if (position.Y < minY) minY = position.Y;
                if (position.Y > maxY) maxY = position.Y;
            }

            if (loadedCells.Count == 0)
            {
                minX = maxX = minY = maxY = 0;
            }
        }

        public void RemoveCell(GridPosition position)
        {
            loadedCells.Remove(position);
        }

        private void BuildLoadedCells(IEnumerable<GridPosition> levelCells)
        {
            if (levelCells != null)
            {
                foreach (GridPosition cell in levelCells)
                {
                    if (IsInsideBounds(cell))
                    {
                        loadedCells.Add(cell);
                    }
                }

                return;
            }

            for (int y = MinY; y <= MaxY; y++)
            {
                for (int x = MinX; x <= MaxX; x++)
                {
                    loadedCells.Add(new GridPosition(x, y));
                }
            }
        }

        private void BuildWalls(IEnumerable<GridPosition> levelWalls)
        {
            if (levelWalls == null)
            {
                return;
            }

            foreach (GridPosition wall in levelWalls)
            {
                authoredWalls.Add(wall);
            }
        }

        private bool IsInsideBounds(GridPosition position)
        {
            return position.X >= MinX && position.X <= MaxX
                && position.Y >= MinY && position.Y <= MaxY;
        }
    }
}
