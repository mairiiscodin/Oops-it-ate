using OopsItAte.Grid;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class LevelSceneSettings : MonoBehaviour
    {
        [Serializable]
        public sealed class DoorLink
        {
            [Tooltip("Door marker used in the tile map (1-9).")]
            [Range(1, 9)] public int marker = 1;

            [Tooltip("Scene path selected by the level editor.")]
            public string targetScenePath = string.Empty;

            public char Marker => (char)('0' + Mathf.Clamp(marker, 1, 9));

            public string TargetSceneName
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(targetScenePath))
                    {
                        return string.Empty;
                    }

                    string normalized = targetScenePath.Replace('\\', '/');
                    int slash = normalized.LastIndexOf('/');
                    int dot = normalized.LastIndexOf('.');
                    int start = slash + 1;
                    int length = dot > start ? dot - start : normalized.Length - start;
                    return normalized.Substring(start, length);
                }
            }
        }

        public GridSettings grid = new GridSettings();
        public GridTileTheme tileTheme;

        [Tooltip("Use '.', '#', '_', S, K, P, B and 1-9. The first line is the top row.")]
        [TextArea(6, 20)]
        public string tileMap = string.Empty;

        [Tooltip("Target scene for each numbered door marker in the tile map.")]
        public DoorLink[] doorLinks = Array.Empty<DoorLink>();

        public string[] GetRows()
        {
            return ReadRows();
        }

        public bool TryGetDoorTarget(char marker, out string targetSceneName)
        {
            if (doorLinks == null)
            {
                targetSceneName = string.Empty;
                return false;
            }

            for (int i = 0; i < doorLinks.Length; i++)
            {
                DoorLink link = doorLinks[i];
                if (link != null && link.Marker == marker)
                {
                    targetSceneName = link.TargetSceneName;
                    return !string.IsNullOrWhiteSpace(targetSceneName);
                }
            }

            targetSceneName = string.Empty;
            return false;
        }

        public bool TryReadTileMap(
            out HashSet<GridPosition> mapCells,
            out HashSet<GridPosition> wallCells,
            out HashSet<GridPosition> borderCells)
        {
            mapCells = new HashSet<GridPosition>();
            wallCells = new HashSet<GridPosition>();
            borderCells = new HashSet<GridPosition>();

            string[] rows = ReadRows();
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
                    if (tile == '_' || char.IsDigit(tile))
                    {
                        borderCells.Add(new GridPosition(x, y));
                        continue;
                    }

                    if (tile != '.' && tile != '#'
                        && tile != 'S' && tile != 'K' && tile != 'P' && tile != 'B')
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

        private string[] ReadRows()
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
