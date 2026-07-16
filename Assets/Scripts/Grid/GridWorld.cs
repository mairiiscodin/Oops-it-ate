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

        private readonly GridCellMap cellMap = new GridCellMap();
        private GridCellView cellView;
        private GridPosition playerPosition;
        private readonly List<VoidPushLayer> voidPushLayers = new List<VoidPushLayer>();
        private Coroutine voidBurpCoroutine;

        private sealed class VoidPushLayer
        {
            public VoidPushLayer(
                GridPosition direction,
                List<GridPosition> filledCells,
                List<GridPosition> emptiedCells,
                List<DoorExit> movedDoors)
            {
                Direction = direction;
                FilledCells = filledCells;
                EmptiedCells = emptiedCells;
                MovedDoors = movedDoors;
            }

            public GridPosition Direction { get; }
            public List<GridPosition> FilledCells { get; }
            public List<GridPosition> EmptiedCells { get; }
            public List<DoorExit> MovedDoors { get; }
        }

        public GridSettings Settings => settings;

        public void Initialize(
            GridSettings gridSettings,
            IEnumerable<GridPosition> levelWalls,
            IEnumerable<GridPosition> levelCells = null)
        {
            settings = gridSettings;
            cellMap.Initialize(settings.width, settings.height, levelWalls, levelCells);
            cellView = new GridCellView(transform, settings, floorColor, wallColor);
            cellView.Draw(cellMap.LoadedCells, cellMap.IsAuthoredWall);
        }

        public bool CanEnter(GridPosition position)
        {
            return cellMap.CanEnter(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return cellMap.IsBlocked(position);
        }

        public bool TryPushVoid(GridPosition voidPosition, GridPosition outwardDirection)
        {
            if (!IsCardinalDirection(outwardDirection)
                || cellMap.IsLoaded(voidPosition)
                || cellMap.IsAuthoredWall(voidPosition)
                || cellMap.IsDynamicBlocker(voidPosition)
                || HasDoorAt(voidPosition))
            {
                return false;
            }

            List<GridPosition> voidLine = FindContiguousVoidLine(voidPosition, outwardDirection);
            if (voidLine.Count == 0 || !CanShiftVoidLine(voidLine, outwardDirection))
            {
                return false;
            }

            List<DoorExit> movedDoors = FindDoorsOnLine(voidLine);
            var emptiedCells = new List<GridPosition>();
            for (int i = 0; i < voidLine.Count; i++)
            {
                GridPosition source = voidLine[i];
                GridPosition destination = source + outwardDirection;

                cellMap.LoadCell(source);
                RefreshCell(source);

                if (cellMap.IsLoaded(destination))
                {
                    emptiedCells.Add(destination);
                    RemoveCell(destination);
                }
            }

            for (int i = 0; i < movedDoors.Count; i++)
            {
                movedDoors[i].Shift(outwardDirection);
            }

            voidPushLayers.Add(new VoidPushLayer(
                outwardDirection,
                voidLine,
                emptiedCells,
                movedDoors));
            RefreshCamera();
            RestartVoidBurpTimer();
            Debug.Log($"The grid pushed a line of {voidLine.Count} empty cell(s) outward.");
            return true;
        }

        private List<GridPosition> FindContiguousVoidLine(
            GridPosition origin,
            GridPosition outwardDirection)
        {
            var result = new List<GridPosition>();
            if (!IsVoidBoundaryCell(origin, outwardDirection))
            {
                return result;
            }

            result.Add(origin);
            GridPosition tangent = outwardDirection.X != 0
                ? new GridPosition(0, 1)
                : new GridPosition(1, 0);
            AddVoidLineSide(result, origin, tangent, outwardDirection);
            AddVoidLineSide(
                result,
                origin,
                new GridPosition(-tangent.X, -tangent.Y),
                outwardDirection);
            return result;
        }

        private void AddVoidLineSide(
            ICollection<GridPosition> result,
            GridPosition origin,
            GridPosition tangent,
            GridPosition outwardDirection)
        {
            GridPosition candidate = origin + tangent;
            while (IsInsideTransverseBounds(candidate, outwardDirection)
                && IsVoidBoundaryCell(candidate, outwardDirection))
            {
                result.Add(candidate);
                candidate += tangent;
            }
        }

        private bool IsVoidBoundaryCell(GridPosition position, GridPosition outwardDirection)
        {
            GridPosition interior = position + new GridPosition(
                -outwardDirection.X,
                -outwardDirection.Y);
            return !cellMap.IsLoaded(position)
                && !cellMap.IsAuthoredWall(position)
                && !cellMap.IsDynamicBlocker(position)
                && cellMap.IsLoaded(interior);
        }

        private bool IsInsideTransverseBounds(
            GridPosition position,
            GridPosition outwardDirection)
        {
            return outwardDirection.X != 0
                ? position.Y >= cellMap.MinY && position.Y <= cellMap.MaxY
                : position.X >= cellMap.MinX && position.X <= cellMap.MaxX;
        }

        private bool CanShiftVoidLine(
            IReadOnlyList<GridPosition> voidLine,
            GridPosition outwardDirection)
        {
            for (int i = 0; i < voidLine.Count; i++)
            {
                GridPosition destination = voidLine[i] + outwardDirection;
                if (cellMap.IsAuthoredWall(destination)
                    || cellMap.IsDynamicBlocker(destination)
                    || destination.Equals(playerPosition)
                    || HasDoorAt(destination))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<DoorExit> FindDoorsOnLine(IReadOnlyList<GridPosition> voidLine)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            var result = new List<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].TouchesAny(voidLine))
                {
                    result.Add(doors[i]);
                }
            }

            return result;
        }

        public void SetPlayerPosition(GridPosition position)
        {
            playerPosition = position;
        }

        public bool TryGetBoundaryDirection(GridPosition position, out GridPosition direction)
        {
            if (cellMap.IsLoaded(position))
            {
                direction = default;
                return false;
            }

            if (position.X == cellMap.MinX - 1 && position.Y >= cellMap.MinY && position.Y <= cellMap.MaxY
                && cellMap.IsLoaded(position + new GridPosition(1, 0)))
            {
                direction = new GridPosition(-1, 0);
                return true;
            }
            if (position.X == cellMap.MaxX + 1 && position.Y >= cellMap.MinY && position.Y <= cellMap.MaxY
                && cellMap.IsLoaded(position + new GridPosition(-1, 0)))
            {
                direction = new GridPosition(1, 0);
                return true;
            }
            if (position.Y == cellMap.MinY - 1 && position.X >= cellMap.MinX && position.X <= cellMap.MaxX
                && cellMap.IsLoaded(position + new GridPosition(0, 1)))
            {
                direction = new GridPosition(0, -1);
                return true;
            }
            if (position.Y == cellMap.MaxY + 1 && position.X >= cellMap.MinX && position.X <= cellMap.MaxX
                && cellMap.IsLoaded(position + new GridPosition(0, -1)))
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
                if (cellMap.IsLoaded(interior))
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
            cellMap.AddDynamicBlocker(position);
        }

        public void RemoveDynamicBlocker(GridPosition position)
        {
            cellMap.RemoveDynamicBlocker(position);
        }

        private void RefreshCell(GridPosition position)
        {
            cellView.SetCell(position, cellMap.IsAuthoredWall(position));
        }

        private void RemoveCell(GridPosition position)
        {
            cellMap.RemoveCell(position);
            cellView.RemoveCell(position);
        }

        private void RestartVoidBurpTimer()
        {
            if (voidBurpCoroutine != null)
            {
                StopCoroutine(voidBurpCoroutine);
            }

            voidBurpCoroutine = StartCoroutine(BurpVoidsOverTime());
        }

        private IEnumerator BurpVoidsOverTime()
        {
            while (voidPushLayers.Count > 0)
            {
                yield return new WaitForSeconds(3f);

                VoidPushLayer layer = voidPushLayers[voidPushLayers.Count - 1];
                while (!CanRestoreVoid(layer))
                {
                    TryClearVoidCell(layer);
                    yield return new WaitForSeconds(0.25f);
                }

                RestoreVoid(layer);
                voidPushLayers.RemoveAt(voidPushLayers.Count - 1);
                Debug.Log("The grid burped and restored the latest empty-space cell.");
            }

            voidBurpCoroutine = null;
        }

        private bool CanRestoreVoid(VoidPushLayer layer)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].TouchesAny(layer.FilledCells))
                {
                    return false;
                }
            }

            for (int i = 0; i < layer.FilledCells.Count; i++)
            {
                GridPosition position = layer.FilledCells[i];
                if (position.Equals(playerPosition)
                    || cellMap.IsDynamicBlocker(position)
                    || cellMap.IsAuthoredWall(position))
                {
                    return false;
                }
            }

            return true;
        }

        private void TryClearVoidCell(VoidPushLayer layer)
        {
            GridPosition inwardDirection = new GridPosition(-layer.Direction.X, -layer.Direction.Y);

            GridMover[] movers = FindObjectsByType<GridMover>();
            for (int i = 0; i < movers.Length; i++)
            {
                GridMover mover = movers[i];
                if (mover.World != this || !layer.FilledCells.Contains(mover.CurrentPosition))
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
                if (box.IsPushable && layer.FilledCells.Contains(box.Position))
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
                if (TouchesAny(body, layer.FilledCells))
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

        private static bool TouchesAny(PetBody body, IReadOnlyList<GridPosition> positions)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (body.Contains(positions[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void RestoreVoid(VoidPushLayer layer)
        {
            GridPosition inwardDirection = new GridPosition(
                -layer.Direction.X,
                -layer.Direction.Y);
            for (int i = 0; i < layer.MovedDoors.Count; i++)
            {
                if (layer.MovedDoors[i] != null)
                {
                    layer.MovedDoors[i].Shift(inwardDirection);
                }
            }

            for (int i = 0; i < layer.FilledCells.Count; i++)
            {
                RemoveCell(layer.FilledCells[i]);
            }

            for (int i = 0; i < layer.EmptiedCells.Count; i++)
            {
                GridPosition position = layer.EmptiedCells[i];
                cellMap.LoadCell(position);
                RefreshCell(position);
            }

            RefreshCamera();
        }

        private static bool HasDoorAt(GridPosition position)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCardinalDirection(GridPosition direction)
        {
            return Mathf.Abs(direction.X) + Mathf.Abs(direction.Y) == 1;
        }

        private void RefreshCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            cellMap.GetLoadedBounds(out int minX, out int maxX, out int minY, out int maxY);
            Vector3 min = settings.GridToWorld(new GridPosition(minX, minY));
            Vector3 max = settings.GridToWorld(new GridPosition(maxX, maxY));
            Vector3 center = (min + max) * 0.5f;
            camera.transform.position = new Vector3(center.x, center.y, camera.transform.position.z);
            camera.orthographicSize = Mathf.Max(
                maxX - minX + 1,
                maxY - minY + 1) * 0.65f;
        }
    }
}
