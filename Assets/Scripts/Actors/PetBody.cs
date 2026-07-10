using System;
using System.Collections.Generic;
using System.Collections;
using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PetBody : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private GridPosition origin;

        private readonly HashSet<GridPosition> bodyCells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, GameObject> visuals = new Dictionary<GridPosition, GameObject>();
        private readonly List<List<GridPosition>> growthLayers = new List<List<GridPosition>>();
        private GridWorld world;
        private Material bodyMaterial;
        private Coroutine burpCoroutine;

        public void Initialize(GridWorld gridWorld, GridPosition startPosition)
        {
            world = gridWorld;
            origin = startPosition;
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

        public bool TryFindGrowthPlan(
            GridPosition playerPosition,
            Func<GridPosition, bool> canPlayerMoveTo,
            out List<GridPosition> growthCells,
            out GridPosition pushDirection,
            out bool shouldPushPlayer)
        {
            ClearBlockers();
            growthCells = new List<GridPosition>();
            var candidates = new HashSet<GridPosition>();
            GridPosition preferredPushDirection = default;
            bool playerWillBeCovered = bodyCells.Contains(playerPosition);
            pushDirection = default;

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
                        if (candidate.Equals(playerPosition))
                        {
                            preferredPushDirection = GetPushDirection(cell, playerPosition);
                            playerWillBeCovered = true;
                        }
                    }
                }
            }

            var growthCellSet = new HashSet<GridPosition>(growthCells);
            shouldPushPlayer = playerWillBeCovered
                && TryFindPushDirection(
                    playerPosition,
                    preferredPushDirection,
                    bodyCells,
                    growthCellSet,
                    canPlayerMoveTo,
                    out pushDirection);

            if (playerWillBeCovered && !shouldPushPlayer)
            {
                growthCells.Clear();
            }

            AddBlockers();
            return growthCells.Count > 0;
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


        public bool TryGrow(IReadOnlyList<GridPosition> growthCells)
        {
            ClearBlockers();

            for (int i = 0; i < growthCells.Count; i++)
            {
                GridPosition cell = growthCells[i];
                if (bodyCells.Contains(cell) || world.IsBlocked(cell))
                {
                    AddBlockers();
                    Debug.Log("Pet cannot grow here.");
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
            Debug.Log($"Pet expanded into {growthCells.Count} cell(s).");
            return true;
        }

        public bool BurpAndShrink()
        {
            if (bodyCells.Count <= 1)
            {
                Debug.Log("Pet burped, but it is already 1x1.");
                return false;
            }

            ClearBlockers();
            bodyCells.Clear();
            bodyCells.Add(origin);
            growthLayers.Clear();
            StopBurpTimer();
            Redraw();
            Debug.Log("Pet burped and shrank to 1x1.");
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
                Debug.Log("Pet burped and lost one growth layer.");
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
            GridPosition playerPosition,
            GridPosition preferredDirection,
            HashSet<GridPosition> currentBodyCells,
            HashSet<GridPosition> growthCells,
            Func<GridPosition, bool> canPlayerMoveTo,
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

                GridPosition target = playerPosition + direction;
                if (!currentBodyCells.Contains(target)
                    && !growthCells.Contains(target)
                    && canPlayerMoveTo(target))
                {
                    pushDirection = direction;
                    return true;
                }
            }

            pushDirection = default;
            return false;
        }

        private void Redraw()
        {
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
                visual.name = $"Pet Body {cell}";
                visual.transform.SetParent(transform);
                visual.transform.position = world.Settings.GridToWorld(cell) + Vector3.back * 0.5f;
                visual.transform.localScale = Vector3.one * world.Settings.cellSize * 0.88f;
                visual.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
                Destroy(visual.GetComponent<Collider>());
                visuals.Add(cell, visual);
            }

            AddBlockers();
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
