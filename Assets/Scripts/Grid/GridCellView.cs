using System;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Grid
{
    internal sealed class GridCellView
    {
        private static readonly GridPosition North = new GridPosition(0, 1);
        private static readonly GridPosition East = new GridPosition(1, 0);
        private static readonly GridPosition South = new GridPosition(0, -1);
        private static readonly GridPosition West = new GridPosition(-1, 0);

        private readonly Transform parent;
        private readonly GridSettings settings;
        private readonly GridTileTheme theme;
        private readonly Color floorColor;
        private readonly Color wallColor;
        private readonly Dictionary<GridPosition, CellVisual> visuals =
            new Dictionary<GridPosition, CellVisual>();

        private Func<GridPosition, bool> isWall;
        private Func<GridPosition, bool> isLoaded;
        private Func<GridPosition, bool> isBorder;
        private Sprite fallbackSprite;

        private sealed class CellVisual
        {
            public GameObject Root;
            public SpriteRenderer Base;
            public SpriteRenderer[] Borders;
        }

        public GridCellView(
            Transform parent,
            GridSettings settings,
            GridTileTheme theme,
            Color floorColor,
            Color wallColor)
        {
            this.parent = parent;
            this.settings = settings;
            this.theme = theme;
            this.floorColor = floorColor;
            this.wallColor = wallColor;
        }

        public void Draw(
            IEnumerable<GridPosition> cells,
            IEnumerable<GridPosition> borders,
            Func<GridPosition, bool> wallLookup,
            Func<GridPosition, bool> loadedLookup,
            Func<GridPosition, bool> borderLookup)
        {
            Clear();
            isWall = wallLookup;
            isLoaded = loadedLookup;
            isBorder = borderLookup;

            var positionSet = new HashSet<GridPosition>(cells);
            positionSet.UnionWith(borders);
            var positions = new List<GridPosition>(positionSet);
            for (int i = 0; i < positions.Count; i++)
            {
                EnsureCell(positions[i]);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                SyncCell(positions[i]);
            }
        }

        public void Refresh(GridPosition position)
        {
            SyncCell(position);
            RefreshNeighbors(position);
        }

        public void RemoveCell(GridPosition position)
        {
            Refresh(position);
        }

        private void EnsureCell(GridPosition position)
        {
            if (visuals.ContainsKey(position))
            {
                return;
            }

            var root = new GameObject($"Grid Cell {position}");
            root.transform.SetParent(parent);
            root.transform.position = settings.GridToWorld(position);

            SpriteRenderer baseRenderer = CreateRenderer("Base", root.transform, -100);
            var borderRenderers = new SpriteRenderer[4];
            for (int i = 0; i < borderRenderers.Length; i++)
            {
                borderRenderers[i] = CreateRenderer($"Border {i + 1}", root.transform, -90 + i);
            }
            visuals.Add(position, new CellVisual
            {
                Root = root,
                Base = baseRenderer,
                Borders = borderRenderers
            });
        }

        private void SyncCell(GridPosition position)
        {
            bool loaded = IsLoaded(position);
            bool border = IsBorder(position);
            if (!loaded && !border)
            {
                DestroyVisual(position);
                return;
            }

            EnsureCell(position);
            CellVisual visual = visuals[position];
            visual.Base.enabled = false;
            DisableBorderRenderers(visual);

            if (border)
            {
                bool hasBorderSprite = theme != null
                    && ApplyBorderSprites(visual, GetBorderNeighborMask(position));
                if (!hasBorderSprite)
                {
                    visual.Base.enabled = true;
                    ApplySprite(visual.Base, theme?.wall, wallColor);
                }

                visual.Root.name = $"Border {position}";
                return;
            }

            bool wall = isWall != null && isWall(position);
            Sprite baseSprite = wall ? theme?.wall : theme?.floor;
            visual.Base.enabled = true;
            ApplySprite(
                visual.Base,
                baseSprite,
                wall ? wallColor : floorColor);
            visual.Root.name = $"{(wall ? "Wall" : "Floor")} {position}";
        }

        private bool ApplyBorderSprites(CellVisual visual, int borderNeighborMask)
        {
            Sprite combinedSprite = theme.GetBorderSprite(borderNeighborMask);
            if (combinedSprite != null)
            {
                visual.Borders[0].enabled = true;
                ApplySprite(visual.Borders[0], combinedSprite, Color.white);
                return true;
            }

            int rendererIndex = 0;
            int[] sideMasks = { 1, 2, 4, 8 };
            for (int i = 0; i < sideMasks.Length; i++)
            {
                int sideMask = sideMasks[i];
                if ((borderNeighborMask & sideMask) == 0)
                {
                    continue;
                }

                Sprite sideSprite = theme.GetSingleSideSprite(sideMask);
                if (sideSprite == null)
                {
                    continue;
                }

                SpriteRenderer renderer = visual.Borders[rendererIndex++];
                renderer.enabled = true;
                ApplySprite(renderer, sideSprite, Color.white);
            }

            return rendererIndex > 0;
        }

        private int GetBorderNeighborMask(GridPosition position)
        {
            int mask = 0;
            if (IsBorder(position + North)) mask |= 1;
            if (IsBorder(position + East)) mask |= 2;
            if (IsBorder(position + South)) mask |= 4;
            if (IsBorder(position + West)) mask |= 8;
            return mask;
        }

        private bool IsLoaded(GridPosition position)
        {
            return isLoaded != null && isLoaded(position);
        }

        private bool IsBorder(GridPosition position)
        {
            return isBorder != null && isBorder(position);
        }

        private void RefreshNeighbors(GridPosition position)
        {
            SyncCell(position + North);
            SyncCell(position + East);
            SyncCell(position + South);
            SyncCell(position + West);
        }

        private static void DisableBorderRenderers(CellVisual visual)
        {
            for (int i = 0; i < visual.Borders.Length; i++)
            {
                visual.Borders[i].enabled = false;
            }
        }

        private void DestroyVisual(GridPosition position)
        {
            if (!visuals.TryGetValue(position, out CellVisual visual))
            {
                return;
            }

            UnityEngine.Object.Destroy(visual.Root);
            visuals.Remove(position);
        }

        private SpriteRenderer CreateRenderer(string label, Transform root, int sortingOrder)
        {
            var child = new GameObject(label);
            child.transform.SetParent(root, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ApplySprite(SpriteRenderer renderer, Sprite sprite, Color fallbackColor)
        {
            bool usesFallback = sprite == null;
            renderer.sprite = usesFallback ? GetFallbackSprite() : sprite;
            renderer.color = usesFallback ? fallbackColor : Color.white;

            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scaleX = settings.cellSize / Mathf.Max(spriteSize.x, 0.0001f);
            float scaleY = settings.cellSize / Mathf.Max(spriteSize.y, 0.0001f);
            renderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            Vector3 boundsCenter = renderer.sprite.bounds.center;
            renderer.transform.localPosition = new Vector3(
                -boundsCenter.x * scaleX,
                -boundsCenter.y * scaleY,
                renderer.transform.localPosition.z);
        }

        private Sprite GetFallbackSprite()
        {
            if (fallbackSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                fallbackSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    texture.width);
                fallbackSprite.name = "Grid Fallback Sprite";
            }

            return fallbackSprite;
        }

        private void Clear()
        {
            foreach (CellVisual visual in visuals.Values)
            {
                UnityEngine.Object.Destroy(visual.Root);
            }

            visuals.Clear();
        }
    }
}
