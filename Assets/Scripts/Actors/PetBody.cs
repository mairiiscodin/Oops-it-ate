using System;
using System.Collections.Generic;
using System.Collections;
using OopsItAte.Grid;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace OopsItAte.Actors
{
    public readonly struct PetBodyMove
    {
        public PetBodyMove(PetBody body, GridPosition direction)
        {
            Body = body;
            Direction = direction;
        }

        public PetBody Body { get; }
        public GridPosition Direction { get; }
    }

    public sealed class PetBody : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private GridPosition origin;
        [SerializeField] private string bodyName = "Pet";
        [Tooltip("Visual shown while this body occupies exactly one cell (for example DogVisual or OvenVisual).")]
        [FormerlySerializedAs("normalDogVisual")]
        [SerializeField] private GameObject normalVisual;

        private readonly HashSet<GridPosition> bodyCells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, GameObject> visuals = new Dictionary<GridPosition, GameObject>();
        private readonly List<List<GridPosition>> growthLayers = new List<List<GridPosition>>();
        private GridWorld world;
        private Material bodyMaterial;
        private Coroutine burpCoroutine;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            Initialize(gridWorld, startPosition, color, bodyName);
        }

        public void Initialize(GridWorld gridWorld, GridPosition startPosition, Color bodyColor, string displayName)
        {
            world = gridWorld;
            origin = startPosition;
            color = bodyColor;
            bodyName = displayName;
            FindNormalVisual();
            bodyCells.Clear();
            bodyCells.Add(origin);
            growthLayers.Clear();
            StopBurpTimer();
            Redraw();
        }

        public bool IsAdjacentTo(GridPosition position)
        {
            foreach (GridPosition cell in bodyCells)
            {
                int distance = Mathf.Abs(cell.X - position.X) + Mathf.Abs(cell.Y - position.Y);
                if (distance == 1)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Contains(GridPosition position)
        {
            return bodyCells.Contains(position);
        }

        public bool TouchesBoundary(bool verticalBoundary, int coordinate)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if ((verticalBoundary ? cell.X : cell.Y) == coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        public bool WouldOccupyAfterShift(GridPosition position, GridPosition direction)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if ((cell + direction).Equals(position))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryShift(GridPosition direction)
        {
            ClearBlockers();
            var shiftedCells = new HashSet<GridPosition>();
            foreach (GridPosition cell in bodyCells)
            {
                GridPosition target = cell + direction;
                if (world.IsBlocked(target))
                {
                    AddBlockers();
                    return false;
                }

                shiftedCells.Add(target);
            }

            bodyCells.Clear();
            foreach (GridPosition cell in shiftedCells)
            {
                bodyCells.Add(cell);
            }

            origin += direction;
            for (int layerIndex = 0; layerIndex < growthLayers.Count; layerIndex++)
            {
                for (int cellIndex = 0; cellIndex < growthLayers[layerIndex].Count; cellIndex++)
                {
                    growthLayers[layerIndex][cellIndex] += direction;
                }
            }

            foreach (GameObject visual in visuals.Values)
            {
                Destroy(visual);
            }
            visuals.Clear();
            Redraw();
            return true;
        }

        internal void SuspendBlockers() => ClearBlockers();
        internal void RestoreBlockers() => AddBlockers();

        private void AddOccupiedCells(HashSet<GridPosition> occupied)
        {
            foreach (GridPosition cell in bodyCells) occupied.Add(cell);
        }

        private bool TryGetOverlap(
            HashSet<GridPosition> currentCells,
            HashSet<GridPosition> newCells,
            out GridPosition overlap)
        {
            foreach (GridPosition cell in bodyCells)
            {
                if (currentCells.Contains(cell) || newCells.Contains(cell))
                {
                    overlap = cell;
                    return true;
                }
            }

            overlap = default;
            return false;
        }

        private bool CanShiftTo(GridPosition direction, HashSet<GridPosition> occupied)
        {
            foreach (GridPosition cell in bodyCells)
            {
                GridPosition target = cell + direction;
                if (world.IsBlocked(target)
                    || (occupied.Contains(target) && !bodyCells.Contains(target)))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryFindGrowthPlan(
            GridPosition playerPosition,
            Func<GridPosition, bool> canPlayerMoveTo,
            out List<GridPosition> growthCells,
            out GridPosition pushDirection,
            out bool shouldPushPlayer,
            out List<PushableBoxMove> boxMoves,
            out List<PetBodyMove> bodyMoves)
        {
            ClearBlockers();
            PushableBox[] boxes = FindObjectsByType<PushableBox>();
            PetBody[] bodies = FindObjectsByType<PetBody>();
            SuspendBoxBlockers(boxes);
            SuspendOtherBodyBlockers(bodies, this);
            growthCells = new List<GridPosition>();
            boxMoves = new List<PushableBoxMove>();
            bodyMoves = new List<PetBodyMove>();
            var candidates = new HashSet<GridPosition>();
            var preferredPushDirections = new Dictionary<GridPosition, GridPosition>();
            bool playerWillBeCovered = bodyCells.Contains(playerPosition);
            pushDirection = default;
            shouldPushPlayer = false;

            try
            {
                foreach (GridPosition cell in bodyCells)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            if (offsetX == 0 && offsetY == 0)
                            {
                                continue;
                            }

                            var offset = new GridPosition(offsetX, offsetY);
                            GridPosition candidate = cell + offset;
                            if (bodyCells.Contains(candidate)
                                || candidates.Contains(candidate)
                                || !CanExpandInto(cell, offset, candidate))
                            {
                                continue;
                            }

                            candidates.Add(candidate);
                            growthCells.Add(candidate);
                            preferredPushDirections[candidate] = GetPushDirection(cell, candidate);
                            if (candidate.Equals(playerPosition))
                            {
                                playerWillBeCovered = true;
                            }
                        }
                    }
                }

                var growthCellSet = new HashSet<GridPosition>(growthCells);
                var occupiedPositions = new HashSet<GridPosition> { playerPosition };
                for (int i = 0; i < boxes.Length; i++)
                {
                    if (boxes[i].IsPushable)
                    {
                        occupiedPositions.Add(boxes[i].Position);
                    }
                }
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] != this) bodies[i].AddOccupiedCells(occupiedPositions);
                }

                var reservedTargets = new HashSet<GridPosition>();
                if (playerWillBeCovered)
                {
                    preferredPushDirections.TryGetValue(playerPosition, out GridPosition preferredPlayerDirection);
                    shouldPushPlayer = TryFindPushDirection(
                        playerPosition,
                        preferredPlayerDirection,
                        bodyCells,
                        growthCellSet,
                        target => canPlayerMoveTo(target)
                            && !occupiedPositions.Contains(target)
                            && !reservedTargets.Contains(target),
                        out pushDirection);

                    if (!shouldPushPlayer)
                    {
                        if (bodyCells.Contains(playerPosition))
                        {
                            growthCells.Clear();
                            return false;
                        }

                        growthCellSet.Remove(playerPosition);
                        growthCells.Remove(playerPosition);
                        RemoveGrowthBesidePlayer(playerPosition, growthCells, growthCellSet);
                        pushDirection = default;
                    }
                    else
                    {
                        reservedTargets.Add(playerPosition + pushDirection);
                    }
                }

                for (int i = 0; i < boxes.Length; i++)
                {
                    PushableBox box = boxes[i];
                    if (!box.IsPushable
                        || (!bodyCells.Contains(box.Position) && !growthCellSet.Contains(box.Position)))
                    {
                        continue;
                    }

                    preferredPushDirections.TryGetValue(box.Position, out GridPosition preferredBoxDirection);
                    if (!TryFindPushDirection(
                        box.Position,
                        preferredBoxDirection,
                        bodyCells,
                        growthCellSet,
                        target => box.CanMoveTo(target)
                            && !occupiedPositions.Contains(target)
                            && !reservedTargets.Contains(target),
                        out GridPosition boxPushDirection))
                    {
                        growthCells.Clear();
                        boxMoves.Clear();
                        shouldPushPlayer = false;
                        pushDirection = default;
                        return false;
                    }

                    boxMoves.Add(new PushableBoxMove(box, boxPushDirection));
                    reservedTargets.Add(box.Position + boxPushDirection);
                }

                for (int i = 0; i < bodies.Length; i++)
                {
                    PetBody otherBody = bodies[i];
                    if (otherBody == this
                        || !otherBody.TryGetOverlap(bodyCells, growthCellSet, out GridPosition overlap))
                    {
                        continue;
                    }

                    preferredPushDirections.TryGetValue(overlap, out GridPosition preferredDirection);
                    occupiedPositions.UnionWith(growthCellSet);
                    occupiedPositions.UnionWith(reservedTargets);
                    if (!TryFindBodyPushDirection(
                        otherBody,
                        preferredDirection,
                        occupiedPositions,
                        out GridPosition bodyPushDirection))
                    {
                        growthCells.Clear();
                        boxMoves.Clear();
                        bodyMoves.Clear();
                        shouldPushPlayer = false;
                        pushDirection = default;
                        return false;
                    }

                    bodyMoves.Add(new PetBodyMove(otherBody, bodyPushDirection));
                    foreach (GridPosition cell in otherBody.bodyCells)
                    {
                        reservedTargets.Add(cell + bodyPushDirection);
                    }
                }

                return growthCells.Count > 0;
            }
            finally
            {
                RestoreBoxBlockers(boxes);
                RestoreOtherBodyBlockers(bodies, this);
                AddBlockers();
            }
        }

        private static bool TryFindBodyPushDirection(
            PetBody body,
            GridPosition preferredDirection,
            HashSet<GridPosition> occupiedPositions,
            out GridPosition pushDirection)
        {
            GridPosition[] directions =
            {
                preferredDirection,
                new GridPosition(-1, 0),
                new GridPosition(0, -1),
                new GridPosition(1, 0),
                new GridPosition(0, 1)
            };

            for (int i = 0; i < directions.Length; i++)
            {
                GridPosition direction = directions[i];
                if (!direction.Equals(default) && body.CanShiftTo(direction, occupiedPositions))
                {
                    pushDirection = direction;
                    return true;
                }
            }

            pushDirection = default;
            return false;
        }

        private static void SuspendOtherBodyBlockers(PetBody[] bodies, PetBody except)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != except) bodies[i].SuspendBlockers();
            }
        }

        private static void RestoreOtherBodyBlockers(PetBody[] bodies, PetBody except)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != except) bodies[i].RestoreBlockers();
            }
        }

        private bool CanExpandInto(GridPosition sourceCell, GridPosition offset, GridPosition candidate)
        {
            if (world.IsBlocked(candidate))
            {
                return false;
            }

            if (offset.X == 0 || offset.Y == 0)
            {
                return true;
            }

            GridPosition horizontalSide = sourceCell + new GridPosition(offset.X, 0);
            GridPosition verticalSide = sourceCell + new GridPosition(0, offset.Y);
            return !bodyCells.Contains(horizontalSide)
                && !bodyCells.Contains(verticalSide)
                && !world.IsBlocked(horizontalSide)
                && !world.IsBlocked(verticalSide);
        }

        private static void RemoveGrowthBesidePlayer(
            GridPosition playerPosition,
            List<GridPosition> growthCells,
            HashSet<GridPosition> growthCellSet)
        {
            for (int i = growthCells.Count - 1; i >= 0; i--)
            {
                GridPosition cell = growthCells[i];
                int distance = Mathf.Abs(cell.X - playerPosition.X)
                    + Mathf.Abs(cell.Y - playerPosition.Y);
                if (distance != 1)
                {
                    continue;
                }

                growthCells.RemoveAt(i);
                growthCellSet.Remove(cell);
            }
        }


        public bool TryGrow(IReadOnlyList<GridPosition> growthCells)
        {
            ClearBlockers();

            for (int i = 0; i < growthCells.Count; i++)
            {
                GridPosition cell = growthCells[i];
                if (bodyCells.Contains(cell) || world.IsBlocked(cell))
                {
                    AddBlockers();
                    Debug.Log($"{bodyName} cannot grow here.");
                    return false;
                }
            }

            for (int i = 0; i < growthCells.Count; i++)
            {
                bodyCells.Add(growthCells[i]);
            }

            growthLayers.Add(new List<GridPosition>(growthCells));
            Redraw();
            RestartBurpTimer();
            Debug.Log($"{bodyName} expanded into {growthCells.Count} cell(s).");
            return true;
        }

        public bool BurpAndShrink()
        {
            if (bodyCells.Count <= 1)
            {
                Debug.Log($"{bodyName} burped, but it is already 1x1.");
                return false;
            }

            ClearBlockers();
            bodyCells.Clear();
            bodyCells.Add(origin);
            growthLayers.Clear();
            StopBurpTimer();
            Redraw();
            Debug.Log($"{bodyName} burped and shrank to 1x1.");
            return true;
        }

        private void RestartBurpTimer()
        {
            StopBurpTimer();
            burpCoroutine = StartCoroutine(BurpOverTime());
        }

        private void StopBurpTimer()
        {
            if (burpCoroutine == null)
            {
                return;
            }

            StopCoroutine(burpCoroutine);
            burpCoroutine = null;
        }

        private IEnumerator BurpOverTime()
        {
            while (growthLayers.Count > 0)
            {
                yield return new WaitForSeconds(3f);

                if (growthLayers.Count == 0)
                {
                    break;
                }

                ClearBlockers();
                List<GridPosition> lastLayer = growthLayers[growthLayers.Count - 1];
                for (int i = 0; i < lastLayer.Count; i++)
                {
                    bodyCells.Remove(lastLayer[i]);
                }

                growthLayers.RemoveAt(growthLayers.Count - 1);
                Redraw();
                Debug.Log($"{bodyName} burped and lost one growth layer.");
            }

            burpCoroutine = null;
        }

        private static GridPosition GetPushDirection(GridPosition bodyCell, GridPosition playerPosition)
        {
            int x = Mathf.Clamp(playerPosition.X - bodyCell.X, -1, 1);
            int y = Mathf.Clamp(playerPosition.Y - bodyCell.Y, -1, 1);

            if (x != 0)
            {
                return new GridPosition(x, 0);
            }

            return new GridPosition(0, y);
        }

        private static bool TryFindPushDirection(
            GridPosition currentPosition,
            GridPosition preferredDirection,
            HashSet<GridPosition> currentBodyCells,
            HashSet<GridPosition> growthCells,
            Func<GridPosition, bool> canMoveTo,
            out GridPosition pushDirection)
        {
            GridPosition[] directions =
            {
                preferredDirection,
                new GridPosition(-1, 0),
                new GridPosition(0, -1),
                new GridPosition(1, 0),
                new GridPosition(0, 1)
            };

            for (int i = 0; i < directions.Length; i++)
            {
                GridPosition direction = directions[i];
                if (direction.Equals(default))
                {
                    continue;
                }

                GridPosition target = currentPosition + direction;
                if (!currentBodyCells.Contains(target)
                    && !growthCells.Contains(target)
                    && canMoveTo(target))
                {
                    pushDirection = direction;
                    return true;
                }
            }

            pushDirection = default;
            return false;
        }

        private static void SuspendBoxBlockers(PushableBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].SuspendBlocker();
            }
        }

        private static void RestoreBoxBlockers(PushableBox[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].RestoreBlocker();
            }
        }

        private void Redraw()
        {
            bool showNormalVisual = normalVisual != null && bodyCells.Count == 1;
            if (normalVisual != null)
            {
                normalVisual.SetActive(showNormalVisual);
            }

            // At 1x1 the authored sprite/animation replaces the generated square completely.
            if (showNormalVisual)
            {
                foreach (GameObject visual in visuals.Values)
                {
                    Destroy(visual);
                }

                visuals.Clear();
                AddBlockers();
                return;
            }

            var removedCells = new List<GridPosition>();
            foreach (GridPosition cell in visuals.Keys)
            {
                if (!bodyCells.Contains(cell))
                {
                    removedCells.Add(cell);
                }
            }

            for (int i = 0; i < removedCells.Count; i++)
            {
                GridPosition cell = removedCells[i];
                Destroy(visuals[cell]);
                visuals.Remove(cell);
            }

            if (bodyMaterial == null)
            {
                bodyMaterial = new Material(FindUnlitShader());
                bodyMaterial.color = color;
            }

            foreach (GridPosition cell in bodyCells)
            {
                if (visuals.ContainsKey(cell))
                {
                    continue;
                }

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.name = $"{bodyName} Body {cell}";
                visual.transform.SetParent(transform);
                visual.transform.position = world.Settings.GridToWorld(cell) + Vector3.back * 0.5f;
                visual.transform.localScale = Vector3.one * world.Settings.cellSize * 0.88f;
                visual.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
                Destroy(visual.GetComponent<Collider>());
                visuals.Add(cell, visual);
            }

            AddBlockers();
        }

        private void FindNormalVisual()
        {
            if (normalVisual != null)
            {
                ConfigureNormalVisualSorting();
                return;
            }

            string preferredName = string.Equals(bodyName, "Kitchen", StringComparison.OrdinalIgnoreCase)
                ? "OvenVisual"
                : "DogVisual";
            Transform authoredVisual = transform.Find(preferredName);
            if (authoredVisual != null)
            {
                normalVisual = authoredVisual.gameObject;
            }

            if (normalVisual == null)
            {
                Animator authoredAnimator = GetComponentInChildren<Animator>(true);
                if (authoredAnimator != null && authoredAnimator.gameObject != gameObject)
                {
                    normalVisual = authoredAnimator.gameObject;
                }
            }

            if (normalVisual == null)
            {
                SpriteRenderer authoredRenderer = GetComponentInChildren<SpriteRenderer>(true);
                if (authoredRenderer != null && authoredRenderer.gameObject != gameObject)
                {
                    normalVisual = authoredRenderer.gameObject;
                }
            }

            ConfigureNormalVisualSorting();
        }

        private void ConfigureNormalVisualSorting()
        {
            if (normalVisual == null)
            {
                return;
            }

            SortingGroup sortingGroup = normalVisual.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = 10;
            }

            SpriteRenderer[] renderers = normalVisual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = 10;
            }
        }

        private void ClearBlockers()
        {
            foreach (GridPosition cell in bodyCells)
            {
                world.RemoveDynamicBlocker(cell);
            }
        }

        private void AddBlockers()
        {
            foreach (GridPosition cell in bodyCells)
            {
                world.AddDynamicBlocker(cell);
            }
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawCube(transform.position, Vector3.one * 0.72f);
        }
    }
}
