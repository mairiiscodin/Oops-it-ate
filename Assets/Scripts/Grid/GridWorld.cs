using System;
using System.Collections;
using System.Collections.Generic;
using OopsItAte.Actors;
using UnityEngine;

namespace OopsItAte.Grid
{
    public sealed class GridWorld : MonoBehaviour
    {
        [SerializeField] private GridSettings settings;
        [SerializeField] private Color floorColor = new Color(0.16f, 0.16f, 0.16f);
        [SerializeField] private Color wallColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly HashSet<GridPosition> blockedCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> authoredWalls = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> loadedCells = new HashSet<GridPosition>();
        private readonly HashSet<GridPosition> dynamicBlockedCells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, MeshRenderer> cellRenderers = new Dictionary<GridPosition, MeshRenderer>();
        private int minX;
        private int maxX;
        private int minY;
        private int maxY;
        private GridPosition playerPosition;
        private readonly List<BoundaryGrowthLayer> boundaryGrowthLayers = new List<BoundaryGrowthLayer>();
        private Coroutine burpCoroutine;

        private readonly struct BoundaryGrowthLayer
        {
            public BoundaryGrowthLayer(GridPosition direction, int coordinate)
            {
                Direction = direction;
                Coordinate = coordinate;
            }

            public GridPosition Direction { get; }
            public int Coordinate { get; }
        }

        public GridSettings Settings => settings;
        public event Action<GridPosition, GridPosition> BoundaryExpanded;

        public void Initialize(
            GridSettings gridSettings,
            IEnumerable<GridPosition> levelWalls,
            IEnumerable<GridPosition> levelCells = null)
        {
            settings = gridSettings;
            minX = 0;
            maxX = settings.width - 1;
            minY = 0;
            maxY = settings.height - 1;
            BuildLoadedCells(levelCells);
            BuildWalls(levelWalls);
            DrawGrid();
        }

        public bool CanEnter(GridPosition position)
        {
            return loadedCells.Contains(position) && !blockedCells.Contains(position) && !dynamicBlockedCells.Contains(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return !loadedCells.Contains(position) || blockedCells.Contains(position) || dynamicBlockedCells.Contains(position);
        }

        public bool TryExpandBoundary(GridPosition unloadedPosition, GridPosition outwardDirection)
        {
            if (outwardDirection.Equals(new GridPosition(-1, 0))
                && unloadedPosition.X == minX - 1
                && unloadedPosition.Y >= minY && unloadedPosition.Y <= maxY)
            {
                minX--;
                LoadVerticalEdge(minX);
            }
            else if (outwardDirection.Equals(new GridPosition(1, 0))
                && unloadedPosition.X == maxX + 1
                && unloadedPosition.Y >= minY && unloadedPosition.Y <= maxY)
            {
                maxX++;
                LoadVerticalEdge(maxX);
            }
            else if (outwardDirection.Equals(new GridPosition(0, -1))
                && unloadedPosition.Y == minY - 1
                && unloadedPosition.X >= minX && unloadedPosition.X <= maxX)
            {
                minY--;
                LoadHorizontalEdge(minY);
            }
            else if (outwardDirection.Equals(new GridPosition(0, 1))
                && unloadedPosition.Y == maxY + 1
                && unloadedPosition.X >= minX && unloadedPosition.X <= maxX)
            {
                maxY++;
                LoadHorizontalEdge(maxY);
            }
            else
            {
                return false;
            }

            RefreshCamera();
            BoundaryExpanded?.Invoke(outwardDirection, unloadedPosition);
            int coordinate = outwardDirection.X != 0 ? unloadedPosition.X : unloadedPosition.Y;
            boundaryGrowthLayers.Add(new BoundaryGrowthLayer(outwardDirection, coordinate));
            RestartBurpTimer();
            Debug.Log("The grid boundary ate the food and loaded more room space.");
            return true;
        }

        public void SetPlayerPosition(GridPosition position)
        {
            playerPosition = position;
        }

        public bool TryGetBoundaryDirection(GridPosition position, out GridPosition direction)
        {
            if (position.X == minX - 1 && position.Y >= minY && position.Y <= maxY)
            {
                direction = new GridPosition(-1, 0);
                return true;
            }
            if (position.X == maxX + 1 && position.Y >= minY && position.Y <= maxY)
            {
                direction = new GridPosition(1, 0);
                return true;
            }
            if (position.Y == minY - 1 && position.X >= minX && position.X <= maxX)
            {
                direction = new GridPosition(0, -1);
                return true;
            }
            if (position.Y == maxY + 1 && position.X >= minX && position.X <= maxX)
            {
                direction = new GridPosition(0, 1);
                return true;
            }

            direction = default;
            return false;
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
            authoredWalls.Clear();

            if (levelWalls == null)
            {
                return;
            }

            foreach (GridPosition wall in levelWalls)
            {
                authoredWalls.Add(wall);
                if (IsInsideCurrentBounds(wall))
                {
                    blockedCells.Add(wall);
                }
            }
        }

        private void BuildLoadedCells(IEnumerable<GridPosition> levelCells)
        {
            loadedCells.Clear();

            if (levelCells != null)
            {
                foreach (GridPosition cell in levelCells)
                {
                    if (IsInsideCurrentBounds(cell))
                    {
                        loadedCells.Add(cell);
                    }
                }

                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    loadedCells.Add(new GridPosition(x, y));
                }
            }
        }

        private void DrawGrid()
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!loadedCells.Contains(position))
                    {
                        continue;
                    }

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
            cellRenderers[position] = renderer;

            Destroy(cell.GetComponent<Collider>());
        }

        private void LoadVerticalEdge(int x)
        {
            for (int y = minY; y <= maxY; y++)
            {
                GridPosition position = new GridPosition(x, y);
                SetCell(position, authoredWalls.Contains(position));
            }
        }

        private void LoadHorizontalEdge(int y)
        {
            for (int x = minX; x <= maxX; x++)
            {
                GridPosition position = new GridPosition(x, y);
                SetCell(position, authoredWalls.Contains(position));
            }
        }

        private void SetCell(GridPosition position, bool isWall)
        {
            loadedCells.Add(position);
            if (isWall)
            {
                blockedCells.Add(position);
            }
            else
            {
                blockedCells.Remove(position);
            }

            if (!cellRenderers.TryGetValue(position, out MeshRenderer renderer))
            {
                CreateCell(position, isWall ? wallColor : floorColor, isWall ? "Wall" : "Floor");
                return;
            }

            renderer.material.color = isWall ? wallColor : floorColor;
            renderer.gameObject.name = $"{(isWall ? "Wall" : "Floor")} {position}";
        }

        private void RemoveCell(GridPosition position)
        {
            loadedCells.Remove(position);
            blockedCells.Remove(position);
            if (!cellRenderers.TryGetValue(position, out MeshRenderer renderer))
            {
                return;
            }

            Destroy(renderer.gameObject);
            cellRenderers.Remove(position);
        }

        private void RestartBurpTimer()
        {
            if (burpCoroutine != null)
            {
                StopCoroutine(burpCoroutine);
            }

            burpCoroutine = StartCoroutine(BurpBoundariesOverTime());
        }

        private IEnumerator BurpBoundariesOverTime()
        {
            while (boundaryGrowthLayers.Count > 0)
            {
                yield return new WaitForSeconds(3f);

                BoundaryGrowthLayer layer = boundaryGrowthLayers[boundaryGrowthLayers.Count - 1];
                while (!CanRemoveBoundaryLayer(layer))
                {
                    TryPushBoundaryOccupants(layer);
                    yield return new WaitForSeconds(0.25f);
                }

                RemoveBoundaryLayer(layer);
                boundaryGrowthLayers.RemoveAt(boundaryGrowthLayers.Count - 1);
                Debug.Log("The grid boundary burped and lost its latest expansion layer.");
            }

            burpCoroutine = null;
        }

        private bool CanRemoveBoundaryLayer(BoundaryGrowthLayer layer)
        {
            if (layer.Direction.X != 0)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    GridPosition position = new GridPosition(layer.Coordinate, y);
                    if (position.Equals(playerPosition) || dynamicBlockedCells.Contains(position))
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int x = minX; x <= maxX; x++)
                {
                    GridPosition position = new GridPosition(x, layer.Coordinate);
                    if (position.Equals(playerPosition) || dynamicBlockedCells.Contains(position))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void TryPushBoundaryOccupants(BoundaryGrowthLayer layer)
        {
            GridPosition inwardDirection = new GridPosition(-layer.Direction.X, -layer.Direction.Y);
            bool verticalBoundary = layer.Direction.X != 0;

            GridMover[] movers = FindObjectsByType<GridMover>();
            for (int i = 0; i < movers.Length; i++)
            {
                GridMover mover = movers[i];
                int coordinate = verticalBoundary ? mover.CurrentPosition.X : mover.CurrentPosition.Y;
                if (mover.World != this || coordinate != layer.Coordinate)
                {
                    continue;
                }

                GridPosition target = mover.CurrentPosition + inwardDirection;
                if (mover.CanMoveTo(target))
                {
                    mover.MoveTo(target);
                }
            }

            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            for (int i = 0; i < boxes.Length; i++)
            {
                PushableBox box = boxes[i];
                int coordinate = verticalBoundary ? box.Position.X : box.Position.Y;
                if (box.IsPushable && coordinate == layer.Coordinate)
                {
                    if (!TryPushPlayerAt(box.Position + inwardDirection, inwardDirection, movers))
                    {
                        continue;
                    }

                    box.TryMove(inwardDirection);
                }
            }

            PetBody[] bodies = FindObjectsByType<PetBody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                PetBody body = bodies[i];
                if (body.TouchesBoundary(verticalBoundary, layer.Coordinate))
                {
                    if (body.WouldOccupyAfterShift(playerPosition, inwardDirection)
                        && !TryPushPlayerAt(playerPosition, inwardDirection, movers))
                    {
                        continue;
                    }

                    body.TryShift(inwardDirection);
                }
            }
        }

        private bool TryPushPlayerAt(
            GridPosition position,
            GridPosition direction,
            GridMover[] movers)
        {
            for (int i = 0; i < movers.Length; i++)
            {
                GridMover mover = movers[i];
                if (mover.World != this || !mover.CurrentPosition.Equals(position))
                {
                    continue;
                }

                GridPosition target = position + direction;
                if (!mover.CanMoveTo(target))
                {
                    return false;
                }

                mover.MoveTo(target);
                return true;
            }

            return true;
        }

        private void RemoveBoundaryLayer(BoundaryGrowthLayer layer)
        {
            if (layer.Direction.X != 0)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    RemoveCell(new GridPosition(layer.Coordinate, y));
                }

                if (layer.Direction.X < 0) minX++;
                else maxX--;
            }
            else
            {
                for (int x = minX; x <= maxX; x++)
                {
                    RemoveCell(new GridPosition(x, layer.Coordinate));
                }

                if (layer.Direction.Y < 0) minY++;
                else maxY--;
            }

            GridPosition inwardDirection = new GridPosition(-layer.Direction.X, -layer.Direction.Y);
            GridPosition previousDoorBoundary = layer.Direction.X != 0
                ? new GridPosition(layer.Coordinate + layer.Direction.X, 0)
                : new GridPosition(0, layer.Coordinate + layer.Direction.Y);
            BoundaryExpanded?.Invoke(inwardDirection, previousDoorBoundary);
            RefreshCamera();
        }

        private bool IsInsideCurrentBounds(GridPosition position)
        {
            return position.X >= minX && position.X <= maxX
                && position.Y >= minY && position.Y <= maxY;
        }

        private void RefreshCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 min = settings.GridToWorld(new GridPosition(minX, minY));
            Vector3 max = settings.GridToWorld(new GridPosition(maxX, maxY));
            Vector3 center = (min + max) * 0.5f;
            camera.transform.position = new Vector3(center.x, center.y, camera.transform.position.z);
            camera.orthographicSize = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 0.65f;
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }
    }
}
