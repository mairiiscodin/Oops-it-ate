using System;
using System.Collections;
using System.Collections.Generic;
using OopsItAte.Actors;
using OopsItAte.Levels;
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

        private sealed class BoundaryGrowthLayer
        {
            public BoundaryGrowthLayer(
                GridPosition direction,
                int coordinate,
                List<GridPosition> addedCells,
                bool changedBounds)
            {
                Direction = direction;
                Coordinate = coordinate;
                AddedCells = addedCells;
                ChangedBounds = changedBounds;
            }

            public GridPosition Direction { get; }
            public int Coordinate { get; }
            public List<GridPosition> AddedCells { get; }
            public bool ChangedBounds { get; }
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
            if (!IsCardinalDirection(outwardDirection)
                || loadedCells.Contains(unloadedPosition)
                || !loadedCells.Contains(unloadedPosition + new GridPosition(
                    -outwardDirection.X,
                    -outwardDirection.Y)))
            {
                return false;
            }

            List<GridPosition> addedCells;
            bool changedBounds = true;
            if (outwardDirection.Equals(new GridPosition(-1, 0))
                && unloadedPosition.X == minX - 1
                && unloadedPosition.Y >= minY && unloadedPosition.Y <= maxY)
            {
                minX--;
                addedCells = LoadVerticalEdge(minX);
            }
            else if (outwardDirection.Equals(new GridPosition(1, 0))
                && unloadedPosition.X == maxX + 1
                && unloadedPosition.Y >= minY && unloadedPosition.Y <= maxY)
            {
                maxX++;
                addedCells = LoadVerticalEdge(maxX);
            }
            else if (outwardDirection.Equals(new GridPosition(0, -1))
                && unloadedPosition.Y == minY - 1
                && unloadedPosition.X >= minX && unloadedPosition.X <= maxX)
            {
                minY--;
                addedCells = LoadHorizontalEdge(minY);
            }
            else if (outwardDirection.Equals(new GridPosition(0, 1))
                && unloadedPosition.Y == maxY + 1
                && unloadedPosition.X >= minX && unloadedPosition.X <= maxX)
            {
                maxY++;
                addedCells = LoadHorizontalEdge(maxY);
            }
            else
            {
                changedBounds = false;
                addedCells = outwardDirection.X != 0
                    ? LoadVerticalEdge(unloadedPosition.X)
                    : LoadHorizontalEdge(unloadedPosition.Y);
                if (addedCells.Count == 0)
                {
                    return false;
                }
            }

            RefreshCamera();
            if (changedBounds)
            {
                BoundaryExpanded?.Invoke(outwardDirection, unloadedPosition);
            }

            int coordinate = outwardDirection.X != 0 ? unloadedPosition.X : unloadedPosition.Y;
            boundaryGrowthLayers.Add(new BoundaryGrowthLayer(
                outwardDirection,
                coordinate,
                addedCells,
                changedBounds));
            RestartBurpTimer();
            Debug.Log($"The grid boundary ate the food and pushed {addedCells.Count} wall cell(s) outward.");
            return true;
        }

        public void SetPlayerPosition(GridPosition position)
        {
            playerPosition = position;
        }

        public bool TryGetBoundaryDirection(GridPosition position, out GridPosition direction)
        {
            if (loadedCells.Contains(position))
            {
                direction = default;
                return false;
            }

            if (position.X == minX - 1 && position.Y >= minY && position.Y <= maxY
                && loadedCells.Contains(position + new GridPosition(1, 0)))
            {
                direction = new GridPosition(-1, 0);
                return true;
            }
            if (position.X == maxX + 1 && position.Y >= minY && position.Y <= maxY
                && loadedCells.Contains(position + new GridPosition(-1, 0)))
            {
                direction = new GridPosition(1, 0);
                return true;
            }
            if (position.Y == minY - 1 && position.X >= minX && position.X <= maxX
                && loadedCells.Contains(position + new GridPosition(0, 1)))
            {
                direction = new GridPosition(0, -1);
                return true;
            }
            if (position.Y == maxY + 1 && position.X >= minX && position.X <= maxX
                && loadedCells.Contains(position + new GridPosition(0, -1)))
            {
                direction = new GridPosition(0, 1);
                return true;
            }

            GridPosition[] directions =
            {
                new GridPosition(-1, 0),
                new GridPosition(1, 0),
                new GridPosition(0, -1),
                new GridPosition(0, 1)
            };
            for (int i = 0; i < directions.Length; i++)
            {
                GridPosition candidate = directions[i];
                GridPosition interior = position + new GridPosition(-candidate.X, -candidate.Y);
                if (loadedCells.Contains(interior))
                {
                    direction = candidate;
                    return true;
                }
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

        private List<GridPosition> LoadVerticalEdge(int x)
        {
            var addedCells = new List<GridPosition>();
            for (int y = minY; y <= maxY; y++)
            {
                GridPosition position = new GridPosition(x, y);
                if (!loadedCells.Contains(position))
                {
                    addedCells.Add(position);
                }

                SetCell(position, authoredWalls.Contains(position));
            }

            return addedCells;
        }

        private List<GridPosition> LoadHorizontalEdge(int y)
        {
            var addedCells = new List<GridPosition>();
            for (int x = minX; x <= maxX; x++)
            {
                GridPosition position = new GridPosition(x, y);
                if (!loadedCells.Contains(position))
                {
                    addedCells.Add(position);
                }

                SetCell(position, authoredWalls.Contains(position));
            }

            return addedCells;
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
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].TouchesAny(layer.AddedCells))
                {
                    return false;
                }
            }

            for (int i = 0; i < layer.AddedCells.Count; i++)
            {
                GridPosition position = layer.AddedCells[i];
                if (position.Equals(playerPosition)
                    || dynamicBlockedCells.Contains(position)
                    || authoredWalls.Contains(position))
                {
                    return false;
                }
            }

            return true;
        }

        private void TryPushBoundaryOccupants(BoundaryGrowthLayer layer)
        {
            GridPosition inwardDirection = new GridPosition(-layer.Direction.X, -layer.Direction.Y);

            GridMover[] movers = FindObjectsByType<GridMover>();
            PushDoorsInward(layer, inwardDirection);
            TryPushAuthoredWalls(layer, inwardDirection, movers);
            for (int i = 0; i < movers.Length; i++)
            {
                GridMover mover = movers[i];
                if (mover.World != this || !layer.AddedCells.Contains(mover.CurrentPosition))
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
                if (box.IsPushable && layer.AddedCells.Contains(box.Position))
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
                if (TouchesBoundaryLayer(body, layer))
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

        private static void PushDoorsInward(
            BoundaryGrowthLayer layer,
            GridPosition inwardDirection)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].TouchesAny(layer.AddedCells))
                {
                    doors[i].Shift(inwardDirection);
                }
            }
        }

        private void TryPushAuthoredWalls(
            BoundaryGrowthLayer layer,
            GridPosition inwardDirection,
            GridMover[] movers)
        {
            for (int i = 0; i < layer.AddedCells.Count; i++)
            {
                GridPosition source = layer.AddedCells[i];
                if (!authoredWalls.Contains(source))
                {
                    continue;
                }

                GridPosition target = source + inwardDirection;
                if (target.Equals(playerPosition)
                    && !TryPushPlayerAt(target, inwardDirection, movers))
                {
                    continue;
                }

                if (!loadedCells.Contains(target)
                    || authoredWalls.Contains(target)
                    || dynamicBlockedCells.Contains(target))
                {
                    continue;
                }

                authoredWalls.Remove(source);
                blockedCells.Remove(source);
                SetCell(source, false);

                authoredWalls.Add(target);
                blockedCells.Add(target);
                SetCell(target, true);
            }
        }

        private static bool TouchesBoundaryLayer(PetBody body, BoundaryGrowthLayer layer)
        {
            for (int i = 0; i < layer.AddedCells.Count; i++)
            {
                if (body.Contains(layer.AddedCells[i]))
                {
                    return true;
                }
            }

            return false;
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
            for (int i = 0; i < layer.AddedCells.Count; i++)
            {
                RemoveCell(layer.AddedCells[i]);
            }

            if (layer.ChangedBounds)
            {
                if (layer.Direction.X < 0)
                {
                    minX++;
                }
                else if (layer.Direction.X > 0)
                {
                    maxX--;
                }
                else if (layer.Direction.Y < 0)
                {
                    minY++;
                }
                else
                {
                    maxY--;
                }

                GridPosition inwardDirection = new GridPosition(-layer.Direction.X, -layer.Direction.Y);
                GridPosition previousDoorBoundary = layer.Direction.X != 0
                    ? new GridPosition(layer.Coordinate + layer.Direction.X, 0)
                    : new GridPosition(0, layer.Coordinate + layer.Direction.Y);
                BoundaryExpanded?.Invoke(inwardDirection, previousDoorBoundary);
            }

            RefreshCamera();
        }

        private static bool IsCardinalDirection(GridPosition direction)
        {
            return Mathf.Abs(direction.X) + Mathf.Abs(direction.Y) == 1;
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
