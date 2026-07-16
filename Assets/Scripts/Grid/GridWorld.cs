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
        [SerializeField] private GridTileTheme tileTheme;
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
                List<GridPosition> addedCornerCells,
                List<GridPosition> emptiedCells,
                List<DoorExit> movedDoors)
            {
                Direction = direction;
                FilledCells = filledCells;
                AddedCornerCells = addedCornerCells;
                EmptiedCells = emptiedCells;
                MovedDoors = movedDoors;
            }

            public GridPosition Direction { get; }
            public List<GridPosition> FilledCells { get; }
            public List<GridPosition> AddedCornerCells { get; }
            public List<GridPosition> EmptiedCells { get; }
            public List<DoorExit> MovedDoors { get; }
        }

        public GridSettings Settings => settings;

        public void Initialize(
            GridSettings gridSettings,
            IEnumerable<GridPosition> levelWalls,
            IEnumerable<GridPosition> levelCells = null,
            IEnumerable<GridPosition> levelBorders = null,
            GridTileTheme theme = null)
        {
            settings = gridSettings;
            tileTheme = theme != null ? theme : tileTheme;
            cellMap.Initialize(
                settings.width,
                settings.height,
                levelWalls,
                levelCells,
                levelBorders);
            cellView = new GridCellView(
                transform,
                settings,
                tileTheme,
                floorColor,
                wallColor);
            cellView.Draw(
                cellMap.LoadedCells,
                cellMap.BorderCells,
                cellMap.IsAuthoredWall,
                cellMap.IsLoaded,
                cellMap.IsBorder);
        }

        public bool CanEnter(GridPosition position)
        {
            return cellMap.CanEnter(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return cellMap.IsBlocked(position);
        }

        public bool TryPushBorder(GridPosition borderPosition, GridPosition outwardDirection)
        {
            if (!IsCardinalDirection(outwardDirection)
                || !cellMap.IsBorder(borderPosition))
            {
                if (IsCardinalDirection(outwardDirection))
                {
                    Debug.LogWarning(
                        $"Cannot push {borderPosition}: that cell is neither an '_' border nor a default outer border.");
                }

                return false;
            }

            List<GridPosition> borderLine = FindContiguousBorderLine(borderPosition, outwardDirection);
            if (borderLine.Count == 0)
            {
                Debug.LogWarning(
                    $"Cannot push border at {borderPosition}: the border has no floor directly behind it.");
                return false;
            }

            List<DoorExit> movedDoors = FindDoorsOnLine(borderLine);
            List<GridPosition> addedCornerCells = FindAddedCornerCells(
                borderLine,
                outwardDirection);
            if (!CanShiftBorderLine(
                borderLine,
                addedCornerCells,
                outwardDirection,
                movedDoors))
            {
                Debug.LogWarning(
                    $"Cannot push border at {borderPosition}: something blocks the row in front of it.");
                return false;
            }

            var emptiedCells = new List<GridPosition>();
            for (int i = 0; i < borderLine.Count; i++)
            {
                GridPosition source = borderLine[i];
                GridPosition destination = source + outwardDirection;

                cellMap.RemoveBorder(source);
                cellMap.LoadCell(source);
                RefreshCell(source);

                if (cellMap.IsLoaded(destination))
                {
                    emptiedCells.Add(destination);
                    RemoveCell(destination);
                }

                cellMap.AddBorder(destination);
                RefreshCell(destination);
            }

            for (int i = 0; i < addedCornerCells.Count; i++)
            {
                GridPosition destination = addedCornerCells[i];
                if (cellMap.IsLoaded(destination))
                {
                    emptiedCells.Add(destination);
                    RemoveCell(destination);
                }

                cellMap.AddBorder(destination);
                RefreshCell(destination);
            }

            for (int i = 0; i < movedDoors.Count; i++)
            {
                movedDoors[i].Shift(outwardDirection);
            }

            voidPushLayers.Add(new VoidPushLayer(
                outwardDirection,
                borderLine,
                addedCornerCells,
                emptiedCells,
                movedDoors));
            RefreshCamera();
            RestartVoidBurpTimer();
            Debug.Log($"The grid pushed a line of {borderLine.Count} border wall cell(s) outward.");
            return true;
        }

        private List<GridPosition> FindContiguousBorderLine(
            GridPosition origin,
            GridPosition outwardDirection)
        {
            var result = new List<GridPosition>();
            if (!IsPushableBorderCell(origin, outwardDirection))
            {
                return result;
            }

            result.Add(origin);
            GridPosition tangent = outwardDirection.X != 0
                ? new GridPosition(0, 1)
                : new GridPosition(1, 0);
            AddBorderLineSide(result, origin, tangent, outwardDirection);
            AddBorderLineSide(
                result,
                origin,
                new GridPosition(-tangent.X, -tangent.Y),
                outwardDirection);
            return result;
        }

        private void AddBorderLineSide(
            ICollection<GridPosition> result,
            GridPosition origin,
            GridPosition tangent,
            GridPosition outwardDirection)
        {
            GridPosition candidate = origin + tangent;
            while (IsInsideTransverseBounds(candidate, outwardDirection)
                && IsPushableBorderCell(candidate, outwardDirection))
            {
                result.Add(candidate);
                candidate += tangent;
            }
        }

        private bool IsPushableBorderCell(GridPosition position, GridPosition outwardDirection)
        {
            GridPosition interior = position + new GridPosition(
                -outwardDirection.X,
                -outwardDirection.Y);
            return cellMap.IsBorder(position)
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

        private bool CanShiftBorderLine(
            IReadOnlyList<GridPosition> borderLine,
            IReadOnlyList<GridPosition> addedCornerCells,
            GridPosition outwardDirection,
            IReadOnlyList<DoorExit> movedDoors)
        {
            for (int i = 0; i < borderLine.Count; i++)
            {
                GridPosition destination = borderLine[i] + outwardDirection;
                if (cellMap.IsAuthoredWall(destination)
                    || cellMap.IsBorder(destination)
                    || cellMap.IsDynamicBlocker(destination)
                    || destination.Equals(playerPosition)
                    || HasUnmovedDoorAt(destination, movedDoors))
                {
                    return false;
                }
            }

            for (int i = 0; i < addedCornerCells.Count; i++)
            {
                if (IsBorderDestinationBlocked(addedCornerCells[i], movedDoors))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBorderDestinationBlocked(
            GridPosition destination,
            IReadOnlyList<DoorExit> movedDoors)
        {
            return cellMap.IsAuthoredWall(destination)
                || cellMap.IsBorder(destination)
                || cellMap.IsDynamicBlocker(destination)
                || destination.Equals(playerPosition)
                || HasUnmovedDoorAt(destination, movedDoors);
        }

        private List<GridPosition> FindAddedCornerCells(
            IReadOnlyList<GridPosition> borderLine,
            GridPosition outwardDirection)
        {
            var result = new List<GridPosition>();
            GridPosition tangent = outwardDirection.X != 0
                ? new GridPosition(0, 1)
                : new GridPosition(1, 0);
            GridPosition negativeTangent = new GridPosition(-tangent.X, -tangent.Y);
            TryAddCornerDestination(result, borderLine, tangent, outwardDirection);
            TryAddCornerDestination(result, borderLine, negativeTangent, outwardDirection);
            return result;
        }

        private void TryAddCornerDestination(
            ICollection<GridPosition> result,
            IReadOnlyList<GridPosition> borderLine,
            GridPosition tangent,
            GridPosition outwardDirection)
        {
            GridPosition extreme = borderLine[0];
            for (int i = 1; i < borderLine.Count; i++)
            {
                GridPosition candidate = borderLine[i];
                if (candidate.X * tangent.X + candidate.Y * tangent.Y
                    > extreme.X * tangent.X + extreme.Y * tangent.Y)
                {
                    extreme = candidate;
                }
            }

            GridPosition existingCorner = extreme + tangent;
            if (cellMap.IsBorder(existingCorner)
                && !IsPushableBorderCell(existingCorner, outwardDirection))
            {
                result.Add(existingCorner + outwardDirection);
            }
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
            cellView.Refresh(position);
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
                Debug.Log("The grid burped and restored the latest border wall line.");
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
                GridPosition source = layer.FilledCells[i];
                GridPosition destination = source + layer.Direction;
                cellMap.AddBorder(source);
                RemoveCell(source);
                cellMap.RemoveBorder(destination);
                RefreshCell(destination);
            }

            for (int i = 0; i < layer.AddedCornerCells.Count; i++)
            {
                GridPosition position = layer.AddedCornerCells[i];
                cellMap.RemoveBorder(position);
                RefreshCell(position);
            }

            for (int i = 0; i < layer.EmptiedCells.Count; i++)
            {
                GridPosition position = layer.EmptiedCells[i];
                cellMap.LoadCell(position);
                RefreshCell(position);
            }

            RefreshCamera();
        }

        private static bool HasUnmovedDoorAt(
            GridPosition position,
            IReadOnlyList<DoorExit> movedDoors)
        {
            DoorExit[] doors = FindObjectsByType<DoorExit>();
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].Contains(position) && !ContainsDoor(movedDoors, doors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDoor(
            IReadOnlyList<DoorExit> doors,
            DoorExit candidate)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i] == candidate)
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
