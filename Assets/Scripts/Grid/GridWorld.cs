using System.Collections;
using System.Collections.Generic;
using OopsItAte.Actors;
using OopsItAte.Audio;
using OopsItAte.Levels;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OopsItAte.Grid
{
    public sealed class GridWorld : MonoBehaviour
    {
        private const float CameraPaddingInCells = 0.5f;

        [SerializeField] private GridSettings settings;
        [SerializeField] private GridTileTheme tileTheme;
        [SerializeField] private Color floorColor = new Color(0.16f, 0.16f, 0.16f);
        [SerializeField] private Color wallColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly GridCellMap cellMap = new GridCellMap();
        private GridCellView cellView;
        private GridPosition playerPosition;
        private readonly List<VoidPushLayer> voidPushLayers = new List<VoidPushLayer>();
        private Coroutine voidBurpCoroutine;
        private Camera fittedCamera;
        private int fittedScreenWidth;
        private int fittedScreenHeight;

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
        public GridTileTheme TileTheme => tileTheme;

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
            FitCameraToLoadedBounds();
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector2Int screenSize = GetScreenSize(camera);
            if (camera != fittedCamera
                || screenSize.x != fittedScreenWidth
                || screenSize.y != fittedScreenHeight)
            {
                FitCameraToLoadedBounds();
            }
        }

        public bool CanEnter(GridPosition position)
        {
            return cellMap.CanEnter(position)
                && !HasBodyAt(position)
                && !HasBoxAt(position);
        }

        public bool CanTraverse(GridPosition source, GridPosition destination)
        {
            int deltaX = destination.X - source.X;
            int deltaY = destination.Y - source.Y;
            if ((deltaX == 0 && deltaY == 0)
                || Mathf.Abs(deltaX) > 1
                || Mathf.Abs(deltaY) > 1
                || !CanEnter(destination))
            {
                return false;
            }

            if (deltaX == 0 || deltaY == 0)
            {
                return true;
            }

            GridPosition horizontalSide = source + new GridPosition(deltaX, 0);
            GridPosition verticalSide = source + new GridPosition(0, deltaY);
            return CanEnter(horizontalSide) && CanEnter(verticalSide);
        }

        public bool IsBlocked(GridPosition position)
        {
            return cellMap.IsBlocked(position);
        }

        public bool IsTerrainBlocked(GridPosition position)
        {
            return cellMap.IsTerrainBlocked(position);
        }

        public bool IsBorder(GridPosition position)
        {
            return cellMap.IsBorder(position);
        }

        private bool HasBodyAt(GridPosition position)
        {
            PetBody[] bodies = FindObjectsByType<PetBody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].Contains(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBoxAt(GridPosition position)
        {
            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].IsInitialized
                    && boxes[i].GrowableBody == null
                    && boxes[i].Position.Equals(position))
                {
                    return true;
                }
            }

            return false;
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
            var pushedBoxes = new List<PushableBox>();
            var pushedBodies = new List<PetBody>();
            if (!CanShiftBorderLine(
                    borderLine,
                    addedCornerCells,
                    outwardDirection,
                    movedDoors)
                || !TryPlanOccupantPushes(
                    borderLine,
                    addedCornerCells,
                    outwardDirection,
                    pushedBoxes,
                    pushedBodies))
            {
                Debug.LogWarning(
                    $"Cannot push border at {borderPosition}: something blocks the row in front of it.");
                return false;
            }

            ApplyOccupantPushes(pushedBoxes, pushedBodies, outwardDirection);

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
            FitCameraToLoadedBounds();
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
            // The line follows the border for as long as there is floor behind it, so an
            // edge that was already widened by an earlier push is still pushed in full.
            GridPosition candidate = origin + tangent;
            while (IsPushableBorderCell(candidate, outwardDirection))
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

        private bool CanShiftBorderLine(
            IReadOnlyList<GridPosition> borderLine,
            IReadOnlyList<GridPosition> addedCornerCells,
            GridPosition outwardDirection,
            IReadOnlyList<DoorExit> movedDoors)
        {
            for (int i = 0; i < borderLine.Count; i++)
            {
                if (IsBorderDestinationBlocked(borderLine[i] + outwardDirection, movedDoors))
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

        // Boxes and bodies standing where the border will land are checked separately by
        // TryPlanOccupantPushes, because they may be pushed out of the way.
        private bool IsBorderDestinationBlocked(
            GridPosition destination,
            IReadOnlyList<DoorExit> movedDoors)
        {
            return cellMap.IsAuthoredWall(destination)
                || cellMap.IsBorder(destination)
                || destination.Equals(playerPosition)
                || HasUnmovedDoorAt(destination, movedDoors);
        }

        // Every box/body on a cell the border moves into is pushed one cell outward, and
        // anything in front of it is pushed along the same way. One stuck object blocks
        // the whole push.
        private bool TryPlanOccupantPushes(
            IReadOnlyList<GridPosition> borderLine,
            IReadOnlyList<GridPosition> addedCornerCells,
            GridPosition outwardDirection,
            List<PushableBox> pushedBoxes,
            List<PetBody> pushedBodies)
        {
            var pending = new Queue<GridPosition>();
            for (int i = 0; i < borderLine.Count; i++)
            {
                pending.Enqueue(borderLine[i] + outwardDirection);
            }

            for (int i = 0; i < addedCornerCells.Count; i++)
            {
                pending.Enqueue(addedCornerCells[i]);
            }

            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            PetBody[] bodies = FindObjectsByType<PetBody>();
            var claimedCells = new HashSet<GridPosition>();
            var occupantCells = new List<GridPosition>();
            while (pending.Count > 0)
            {
                GridPosition cell = pending.Dequeue();
                if (claimedCells.Contains(cell) || !cellMap.IsDynamicBlocker(cell))
                {
                    continue;
                }

                occupantCells.Clear();
                if (!TryClaimOccupantAt(cell, boxes, bodies, pushedBoxes, pushedBodies, occupantCells))
                {
                    return false;
                }

                claimedCells.UnionWith(occupantCells);
                for (int i = 0; i < occupantCells.Count; i++)
                {
                    GridPosition target = occupantCells[i] + outwardDirection;
                    if (claimedCells.Contains(target))
                    {
                        continue;
                    }

                    if (!cellMap.IsLoaded(target)
                        || cellMap.IsAuthoredWall(target)
                        || cellMap.IsBorder(target)
                        || target.Equals(playerPosition))
                    {
                        return false;
                    }

                    if (cellMap.IsDynamicBlocker(target))
                    {
                        pending.Enqueue(target);
                    }
                }
            }

            return true;
        }

        private static bool TryClaimOccupantAt(
            GridPosition cell,
            PushableBox[] boxes,
            PetBody[] bodies,
            List<PushableBox> pushedBoxes,
            List<PetBody> pushedBodies,
            List<GridPosition> occupantCells)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].IsPushable && boxes[i].Position.Equals(cell))
                {
                    pushedBoxes.Add(boxes[i]);
                    occupantCells.Add(cell);
                    return true;
                }
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].Contains(cell))
                {
                    if (!bodies[i].CanBePushedByBodyGrowth)
                    {
                        return false;
                    }

                    pushedBodies.Add(bodies[i]);
                    occupantCells.AddRange(bodies[i].Cells);
                    return true;
                }
            }

            return false;
        }

        private static void ApplyOccupantPushes(
            IReadOnlyList<PushableBox> pushedBoxes,
            IReadOnlyList<PetBody> pushedBodies,
            GridPosition outwardDirection)
        {
            // Lift every blocker first so objects in a chain cannot erase each other's
            // blocker while they move; bodies re-add theirs at the end.
            for (int i = 0; i < pushedBoxes.Count; i++)
            {
                pushedBoxes[i].SuspendBlocker();
            }

            for (int i = 0; i < pushedBodies.Count; i++)
            {
                pushedBodies[i].SuspendBlockers();
            }

            for (int i = 0; i < pushedBodies.Count; i++)
            {
                pushedBodies[i].ShiftUnchecked(outwardDirection);
            }

            for (int i = 0; i < pushedBoxes.Count; i++)
            {
                pushedBoxes[i].SetPositionUnchecked(pushedBoxes[i].Position + outwardDirection);
            }

            for (int i = 0; i < pushedBodies.Count; i++)
            {
                pushedBodies[i].RestoreBlockers();
            }
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
            GridPosition cornerDestination = existingCorner + outwardDirection;
            // A corner that already meets another border (e.g. an inner '_' wall touching
            // the outer edge) is closed as it is and needs no extra cell.
            if (cellMap.IsBorder(existingCorner)
                && !IsPushableBorderCell(existingCorner, outwardDirection)
                && !cellMap.IsBorder(cornerDestination))
            {
                result.Add(cornerDestination);
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
                SoundManager.Play(Sfx.Burp, 0.8f);
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

            FitCameraToLoadedBounds();
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

        public void FitCameraToLoadedBounds()
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

            float cellSize = settings.cellSize;
            float contentWidth = (maxX - minX + 1) * cellSize;
            float contentHeight = (maxY - minY + 1) * cellSize;
            float padding = CameraPaddingInCells * cellSize;
            float paddedWidth = contentWidth + padding * 2f;
            float paddedHeight = contentHeight + padding * 2f;
            Vector2Int screenSize = GetScreenSize(camera);
            float safeAspect = Mathf.Max((float)screenSize.x / Mathf.Max(1, screenSize.y), 0.01f);

            float fittedSize = Mathf.Max(
                paddedHeight * 0.5f,
                paddedWidth / (safeAspect * 2f));

            camera.orthographic = true;
            camera.orthographicSize = fittedSize;

            // A fractional zoom (e.g. after a border push) draws each 32px tile on a
            // non-integer number of screen pixels, which shows up as seams/stripes
            // between tiles. Instead the room is rendered at 1:1 art pixels into a small
            // texture (no seams) and PixelArtUpscaler draws that texture over the whole
            // screen with a sharp-upscale shader: it fills the window, stays crisp, and
            // has none of the stripes plain point filtering gives at fractional scales.
            int artPixelsPerUnit = GetArtPixelsPerUnit();
            PixelPerfectCamera pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
            if (pixelPerfect != null)
            {
                pixelPerfect.enabled = false;
            }

            PixelArtUpscaler upscaler = camera.GetComponent<PixelArtUpscaler>();
            if (artPixelsPerUnit > 0)
            {
                // Even sizes keep the texture edges on the art pixel grid once the
                // camera centre is snapped to it.
                int referenceHeight = MakeEven(Mathf.CeilToInt(fittedSize * 2f * artPixelsPerUnit));
                int referenceWidth = MakeEven(Mathf.RoundToInt(referenceHeight * safeAspect));
                upscaler = PixelArtUpscaler.GetOrAdd(camera);
                upscaler.enabled = true;
                upscaler.Configure(referenceWidth, referenceHeight, artPixelsPerUnit);
            }
            else if (upscaler != null)
            {
                upscaler.enabled = false;
            }

            fittedCamera = camera;
            fittedScreenWidth = screenSize.x;
            fittedScreenHeight = screenSize.y;
        }

        // Screen size rather than the camera's own size: the camera renders into the
        // PixelArtUpscaler texture, whose size is derived from this.
        private static Vector2Int GetScreenSize(Camera camera)
        {
            return new Vector2Int(Screen.width, Screen.height);
        }

        /// <summary>
        /// Art pixels per world unit, taken from the floor tile drawn over one cell.
        /// Returns 0 when there is no whole-number value to render pixel-perfectly at.
        /// </summary>
        private int GetArtPixelsPerUnit()
        {
            Sprite referenceTile = tileTheme != null ? tileTheme.floor : null;
            if (referenceTile == null || settings.cellSize <= 0f)
            {
                return 0;
            }

            float pixelsPerUnit = referenceTile.rect.height / settings.cellSize;
            int rounded = Mathf.RoundToInt(pixelsPerUnit);
            return rounded >= 1 && Mathf.Abs(pixelsPerUnit - rounded) < 0.001f ? rounded : 0;
        }

        private static int MakeEven(int value)
        {
            return value + (value & 1);
        }    }
}
