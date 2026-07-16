using System.Collections.Generic;

namespace OopsItAte.Grid
{
    internal sealed class GridCellMap
    {
        private readonly HashSet<GridPosition> loadedCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> authoredWalls = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> borderCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> dynamicBlockers = new HashSet<GridPosition>();

        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public int MinY { get; private set; }
        public int MaxY { get; private set; }
        public IEnumerable<GridPosition> LoadedCells => loadedCells;
        public IEnumerable<GridPosition> BorderCells => borderCells;

        public void Initialize(
            int width,
            int height,
            IEnumerable<GridPosition> levelWalls,
            IEnumerable<GridPosition> levelCells,
            IEnumerable<GridPosition> levelBorders)
        {
            MinX = 0;
            MaxX = width - 1;
            MinY = 0;
            MaxY = height - 1;

            loadedCells.Clear();
            authoredWalls.Clear();
            borderCells.Clear();
            dynamicBlockers.Clear();

            BuildLoadedCells(levelCells);
            BuildWalls(levelWalls);
            BuildBorders(levelBorders);
            BuildDefaultOuterBorder();
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
        public bool IsBorder(GridPosition position) => borderCells.Contains(position);
        public bool IsDynamicBlocker(GridPosition position) => dynamicBlockers.Contains(position);

        public void AddDynamicBlocker(GridPosition position) => dynamicBlockers.Add(position);
        public void RemoveDynamicBlocker(GridPosition position) => dynamicBlockers.Remove(position);

        public bool LoadCell(GridPosition position)
        {
            return loadedCells.Add(position);
        }

        public void AddBorder(GridPosition position) => borderCells.Add(position);
        public void RemoveBorder(GridPosition position) => borderCells.Remove(position);

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

            foreach (GridPosition position in borderCells)
            {
                if (position.X < minX) minX = position.X;
                if (position.X > maxX) maxX = position.X;
                if (position.Y < minY) minY = position.Y;
                if (position.Y > maxY) maxY = position.Y;
            }

            if (loadedCells.Count == 0 && borderCells.Count == 0)
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

        private void BuildBorders(IEnumerable<GridPosition> levelBorders)
        {
            if (levelBorders == null)
            {
                return;
            }

            foreach (GridPosition border in levelBorders)
            {
                borderCells.Add(border);
            }
        }

        private void BuildDefaultOuterBorder()
        {
            int left = MinX - 1;
            int right = MaxX + 1;
            int bottom = MinY - 1;
            int top = MaxY + 1;

            for (int y = bottom; y <= top; y++)
            {
                borderCells.Add(new GridPosition(left, y));
                borderCells.Add(new GridPosition(right, y));
            }

            for (int x = MinX; x <= MaxX; x++)
            {
                borderCells.Add(new GridPosition(x, bottom));
                borderCells.Add(new GridPosition(x, top));
            }
        }

        private bool IsInsideBounds(GridPosition position)
        {
            return position.X >= MinX && position.X <= MaxX
                && position.Y >= MinY && position.Y <= MaxY;
        }
    }
}
