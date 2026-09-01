using System.Collections;
using System.Collections.Generic;
using OopsItAte.Grid;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OopsItAte.Levels
{
    public enum DoorDirection
    {
        Auto = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

    public sealed class DoorExit : MonoBehaviour
    {
        [SerializeField] private string targetSceneName;
        [SerializeField] private string roomId;
        [SerializeField] private string doorId;
        [SerializeField] private string targetDoorId;
        [SerializeField] private bool startsLocked;
        [SerializeField] private bool requiresFood;
        [SerializeField] private GridPosition position;
        [SerializeField] private DoorDirection openingDirection = DoorDirection.Auto;
        [Header("Directional Door Sprites")]
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite downSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;
        [SerializeField] private Color color = new Color(0.9f, 0.15f, 0.15f);

        public string TargetSceneName => targetSceneName;
        public string RoomId => roomId;
        public string DoorId => doorId;
        public string TargetDoorId => targetDoorId;
        public bool RequiresFood => requiresFood;
        public bool IsLocked => startsLocked && !GameSession.IsDoorUnlocked(roomId, doorId);
        public GridPosition Position => position;
        public DoorDirection OpeningDirection => openingDirection;

        public void SetTargetScene(string sceneName)
        {
            targetSceneName = sceneName == null ? string.Empty : sceneName.Trim();
        }

        public void SetOpeningDirection(DoorDirection direction)
        {
            openingDirection = direction;
        }

        public void ConfigureAdventure(
            string owningRoomId,
            string localDoorId,
            string arrivalDoorId,
            bool locked,
            bool foodRequired)
        {
            roomId = NormalizeId(owningRoomId);
            doorId = NormalizeId(localDoorId);
            targetDoorId = NormalizeId(arrivalDoorId);
            startsLocked = locked;
            requiresFood = foodRequired;
        }

        public bool CanOpenFrom(GridPosition playerPosition, GridPosition movementDirection)
        {
            GridPosition openVector = GetDirectionVector(openingDirection);
            if (!movementDirection.Equals(openVector))
            {
                return false;
            }

            // The player must still be on the adjacent interior cell when the door is
            // checked. This prevents a door from triggering after an unrelated move.
            return cells.Contains(playerPosition + movementDirection);
        }

        public bool TryGetInteriorPosition(out GridPosition interiorPosition)
        {
            GridPosition openVector = GetDirectionVector(openingDirection);
            if (!openVector.Equals(default))
            {
                interiorPosition = new GridPosition(
                    position.X - openVector.X,
                    position.Y - openVector.Y);
                return true;
            }

            interiorPosition = default;
            return false;
        }

        private GridSettings grid;
        private GridWorld world;
        private GridPosition boundaryDirection;
        private readonly HashSet<GridPosition> cells = new HashSet<GridPosition>();
        private readonly Dictionary<GridPosition, GameObject> visuals = new Dictionary<GridPosition, GameObject>();
        private readonly List<List<GridPosition>> growthLayers = new List<List<GridPosition>>();
        private Coroutine burpCoroutine;

        public void Initialize(GridWorld gridWorld)
        {
            world = gridWorld;
            grid = world.Settings;
            position = grid.WorldToGrid(transform.position);
            if (string.IsNullOrWhiteSpace(doorId)) doorId = gameObject.name;
            cells.Clear();
            cells.Add(position);
            world.TryGetBoundaryDirection(position, out boundaryDirection);
            if (openingDirection == DoorDirection.Auto)
            {
                openingDirection = FromVector(boundaryDirection);
            }
            Redraw();
        }

        public bool Contains(GridPosition gridPosition) => cells.Contains(gridPosition);

        public bool TouchesAny(IReadOnlyList<GridPosition> positions)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (cells.Contains(positions[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void Shift(GridPosition direction)
        {
            position += direction;
            var movedCells = new HashSet<GridPosition>();
            foreach (GridPosition cell in cells)
            {
                movedCells.Add(cell + direction);
            }

            cells.Clear();
            foreach (GridPosition cell in movedCells)
            {
                cells.Add(cell);
            }

            for (int layer = 0; layer < growthLayers.Count; layer++)
            {
                for (int i = 0; i < growthLayers[layer].Count; i++)
                {
                    growthLayers[layer][i] += direction;
                }
            }

            Redraw();
        }

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

            Shift(direction);
            boundaryDirection = new GridPosition(-direction.X, -direction.Y).Equals(boundaryDirection)
                ? boundaryDirection
                : direction;
        }

        private void EnsureVisual()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
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

            Sprite sprite = GetCurrentSprite();
            Sprite floorSprite = world.TileTheme != null ? world.TileTheme.floor : null;
            foreach (GridPosition cell in cells)
            {
                if (!visuals.TryGetValue(cell, out GameObject visual))
                {
                    visual = new GameObject($"Door {cell}");
                    visual.transform.SetParent(transform);
                    if (floorSprite != null)
                    {
                        CreateSpriteLayer("Floor", visual.transform, floorSprite, -80);
                    }
                    if (sprite != null)
                    {
                        CreateSpriteLayer("Door", visual.transform, sprite, 20);
                    }
                    visuals[cell] = visual;
                }
                visual.transform.position = grid.GridToWorld(cell) + Vector3.back * 0.2f;
            }
        }

        private void CreateSpriteLayer(
            string layerName,
            Transform parent,
            Sprite sprite,
            int sortingOrder)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(parent, false);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            Vector2 size = sprite.bounds.size;
            float scaleX = grid.cellSize / Mathf.Max(size.x, 0.0001f);
            float scaleY = grid.cellSize / Mathf.Max(size.y, 0.0001f);
            layer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            Vector3 center = sprite.bounds.center;
            layer.transform.localPosition = new Vector3(
                -center.x * scaleX,
                -center.y * scaleY,
                0f);
        }

        private Sprite GetCurrentSprite()
        {
            GridTileTheme theme = world != null ? world.TileTheme : null;
            switch (openingDirection)
            {
                case DoorDirection.Up: return upSprite != null ? upSprite : theme?.doorUp;
                case DoorDirection.Down: return downSprite != null ? downSprite : theme?.doorDown;
                case DoorDirection.Left: return leftSprite != null ? leftSprite : theme?.doorLeft;
                case DoorDirection.Right: return rightSprite != null ? rightSprite : theme?.doorRight;
                default: return null;
            }
        }

        private static GridPosition GetDirectionVector(DoorDirection direction)
        {
            switch (direction)
            {
                case DoorDirection.Up: return new GridPosition(0, 1);
                case DoorDirection.Down: return new GridPosition(0, -1);
                case DoorDirection.Left: return new GridPosition(-1, 0);
                case DoorDirection.Right: return new GridPosition(1, 0);
                default: return default;
            }
        }

        private static DoorDirection FromVector(GridPosition direction)
        {
            if (direction.Equals(new GridPosition(0, 1))) return DoorDirection.Up;
            if (direction.Equals(new GridPosition(0, -1))) return DoorDirection.Down;
            if (direction.Equals(new GridPosition(-1, 0))) return DoorDirection.Left;
            if (direction.Equals(new GridPosition(1, 0))) return DoorDirection.Right;
            return DoorDirection.Auto;
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

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            MeshRenderer placeholder = GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
            if (upSprite == null) upSprite = LoadSprite("Assets/Assets/DoorUp.aseprite", "DoorUp");
            if (downSprite == null) downSprite = LoadSprite("Assets/Assets/DoorDown.aseprite", "DoorDown");
            if (leftSprite == null) leftSprite = LoadSprite("Assets/Assets/DoorLeft.aseprite", "DoorLeft");
            if (rightSprite == null) rightSprite = LoadSprite("Assets/Assets/DoorRight.aseprite", "DoorRight");
#endif
        }

#if UNITY_EDITOR
        private static Sprite LoadSprite(string path, string spriteName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Sprite first = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Sprite sprite)) continue;
                if (first == null) first = sprite;
                if (sprite.name == spriteName) return sprite;
            }
            return first;
        }
#endif

    }
}
