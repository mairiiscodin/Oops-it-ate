using OopsItAte.Grid;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class LevelSceneSettings : MonoBehaviour
    {
        public GridSettings grid = new GridSettings();
        public GridTileTheme tileTheme;

        [Tooltip("Optional tile-shaped map. Use '.' for floor, '#' for solid wall, '_' for a pushable border wall, and spaces for invisible space outside the map. The first line is the top row.")]
        [TextArea(6, 20)]
        public string tileMap = string.Empty;

        public bool TryReadTileMap(
            out HashSet<GridPosition> mapCells,
            out HashSet<GridPosition> wallCells,
            out HashSet<GridPosition> borderCells)
        {
            mapCells = new HashSet<GridPosition>();
            wallCells = new HashSet<GridPosition>();
            borderCells = new HashSet<GridPosition>();

            string[] rows = GetRows();
            if (rows.Length == 0)
            {
                return false;
            }

            int height = rows.Length;
            int width = 0;
            for (int row = 0; row < height; row++)
            {
                width = Mathf.Max(width, rows[row].Length);
            }

            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < rows[row].Length; x++)
                {
                    char tile = rows[row][x];
                    if (tile == '_')
                    {
                        borderCells.Add(new GridPosition(x, y));
                        continue;
                    }

                    if (tile != '.' && tile != '#')
                    {
                        continue;
                    }

                    var position = new GridPosition(x, y);
                    mapCells.Add(position);
                    if (tile == '#')
                    {
                        wallCells.Add(position);
                    }
                }
            }

            if (mapCells.Count == 0)
            {
                return false;
            }

            grid.width = Mathf.Max(1, width);
            grid.height = Mathf.Max(1, height);
            return true;
        }

        private string[] GetRows()
        {
            if (string.IsNullOrWhiteSpace(tileMap))
            {
                return System.Array.Empty<string>();
            }

            string normalized = tileMap.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] allRows = normalized.Split('\n');
            int first = 0;
            int last = allRows.Length - 1;

            while (first <= last && string.IsNullOrWhiteSpace(allRows[first])) first++;
            while (last >= first && string.IsNullOrWhiteSpace(allRows[last])) last--;

            if (first > last)
            {
                return System.Array.Empty<string>();
            }

            int commonIndent = int.MaxValue;
            for (int i = first; i <= last; i++)
            {
                if (string.IsNullOrWhiteSpace(allRows[i]))
                {
                    continue;
                }

                int indent = 0;
                while (indent < allRows[i].Length && char.IsWhiteSpace(allRows[i][indent])) indent++;
                commonIndent = Mathf.Min(commonIndent, indent);
            }

            commonIndent = commonIndent == int.MaxValue ? 0 : commonIndent;
            string[] rows = new string[last - first + 1];
            for (int i = 0; i < rows.Length; i++)
            {
                string row = allRows[first + i];
                rows[i] = row.Length >= commonIndent ? row.Substring(commonIndent) : string.Empty;
            }

            return rows;
        }
    }
}
