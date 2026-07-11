using System.Collections;
using System.Collections.Generic;
using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class DoorExit : MonoBehaviour
    {
        [SerializeField] private string targetSceneName;
        [SerializeField] private GridPosition position;
        [SerializeField] private Color color = new Color(0.9f, 0.15f, 0.15f);

        public string TargetSceneName => targetSceneName;
        public GridPosition Position => position;

        private GridSettings grid;
        private GridWorld world;
        private GridPosition boundaryDirection;
        private readonly HashSet<GridPosition> cells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, GameObject> visuals = new Dictionary<GridPosition, GameObject>();
        private readonly List<List<GridPosition>> growthLayers = new List<List<GridPosition>>();
        private Material doorMaterial;
        private Coroutine burpCoroutine;

        public void Initialize(GridWorld gridWorld)
        {
            world = gridWorld;
            grid = world.Settings;
            position = grid.WorldToGrid(transform.position);
            cells.Clear();
            cells.Add(position);
            world.TryGetBoundaryDirection(position, out boundaryDirection);
            Redraw();
        }

        public bool Contains(GridPosition gridPosition) => cells.Contains(gridPosition);

        public bool FeedAndGrow()
        {
            if (boundaryDirection.Equals(default))
            {
                Debug.LogWarning($"Door {name} must be placed on the unloaded grid boundary to grow.");
                return false;
            }

            GridPosition tangent = boundaryDirection.X != 0
                ? new GridPosition(0, 1)
                : new GridPosition(1, 0);
            var addedCells = new List<GridPosition>();
            TryAddBoundaryCell(GetExtremeCell(tangent) + tangent, addedCells);
            TryAddBoundaryCell(GetExtremeCell(new GridPosition(-tangent.X, -tangent.Y))
                + new GridPosition(-tangent.X, -tangent.Y), addedCells);

            if (addedCells.Count == 0)
            {
                return false;
            }

            growthLayers.Add(addedCells);
            Redraw();
            RestartBurpTimer();
            return true;
        }

        public void MoveWithBoundary(GridPosition direction, GridPosition previousBoundaryPosition)
        {
            bool isSameEdge = direction.X != 0
                ? position.X == previousBoundaryPosition.X
                : position.Y == previousBoundaryPosition.Y;
            if (!isSameEdge)
            {
                return;
            }

            position += direction;
            var movedCells = new HashSet<GridPosition>();
            foreach (GridPosition cell in cells)
            {
                movedCells.Add(cell + direction);
            }
            cells.Clear();
            foreach (GridPosition cell in movedCells) cells.Add(cell);
            for (int layer = 0; layer < growthLayers.Count; layer++)
            {
                for (int i = 0; i < growthLayers[layer].Count; i++)
                {
                    growthLayers[layer][i] += direction;
                }
            }
            boundaryDirection = new GridPosition(-direction.X, -direction.Y).Equals(boundaryDirection)
                ? boundaryDirection
                : direction;
            Redraw();
        }

        private void EnsureVisual()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (GetComponent<MeshFilter>() == null)
            {
                gameObject.AddComponent<MeshFilter>().mesh = CreateQuadMesh();
            }

            renderer.enabled = false;
        }

        private void Redraw()
        {
            EnsureVisual();
            var removed = new List<GridPosition>();
            foreach (GridPosition cell in visuals.Keys)
            {
                if (!cells.Contains(cell)) removed.Add(cell);
            }
            for (int i = 0; i < removed.Count; i++)
            {
                Destroy(visuals[removed[i]]);
                visuals.Remove(removed[i]);
            }

            if (doorMaterial == null)
            {
                doorMaterial = new Material(FindUnlitShader());
                doorMaterial.color = color;
            }
            foreach (GridPosition cell in cells)
            {
                if (!visuals.TryGetValue(cell, out GameObject visual))
                {
                    visual = new GameObject($"Door {cell}");
                    visual.transform.SetParent(transform);
                    visual.AddComponent<MeshFilter>().sharedMesh = CreateQuadMesh();
                    visual.AddComponent<MeshRenderer>().sharedMaterial = doorMaterial;
                    visuals[cell] = visual;
                }
                visual.transform.position = grid.GridToWorld(cell) + Vector3.back * 0.2f;
                visual.transform.localScale = Vector3.one * grid.cellSize * 0.92f;
            }
        }

        private GridPosition GetExtremeCell(GridPosition direction)
        {
            GridPosition result = position;
            foreach (GridPosition cell in cells)
            {
                if (cell.X * direction.X + cell.Y * direction.Y
                    > result.X * direction.X + result.Y * direction.Y)
                {
                    result = cell;
                }
            }
            return result;
        }

        private void TryAddBoundaryCell(GridPosition candidate, List<GridPosition> addedCells)
        {
            if (!cells.Contains(candidate)
                && world.TryGetBoundaryDirection(candidate, out GridPosition direction)
                && direction.Equals(boundaryDirection))
            {
                cells.Add(candidate);
                addedCells.Add(candidate);
            }
        }

        private void RestartBurpTimer()
        {
            if (burpCoroutine != null) StopCoroutine(burpCoroutine);
            burpCoroutine = StartCoroutine(BurpOverTime());
        }

        private IEnumerator BurpOverTime()
        {
            while (growthLayers.Count > 0)
            {
                yield return new WaitForSeconds(3f);
                List<GridPosition> layer = growthLayers[growthLayers.Count - 1];
                for (int i = 0; i < layer.Count; i++) cells.Remove(layer[i]);
                growthLayers.RemoveAt(growthLayers.Count - 1);
                Redraw();
            }
            burpCoroutine = null;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
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
            Gizmos.DrawCube(transform.position, Vector3.one * 0.92f);
        }
    }
}
