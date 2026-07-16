using OopsItAte.Levels;
using UnityEditor;
using UnityEngine;

namespace OopsItAte.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Apply Text Map"))
            {
                ApplyTextMap((LevelDefinition)target);
            }
        }

        private static void ApplyTextMap(LevelDefinition level)
        {
            Undo.RecordObject(level, "Apply Level Text Map");

            string[] rawRows = level.textMap.Replace("\r\n", "\n").Split('\n');
            int height = rawRows.Length;
            int width = 0;

            for (int i = 0; i < rawRows.Length; i++)
            {
                width = Mathf.Max(width, rawRows[i].Length);
            }

            var walls = new System.Collections.Generic.List<Vector2Int>();
            var cells = new System.Collections.Generic.List<Vector2Int>();
            var borders = new System.Collections.Generic.List<Vector2Int>();

            for (int row = 0; row < rawRows.Length; row++)
            {
                string line = rawRows[row];
                int y = height - 1 - row;

                for (int x = 0; x < line.Length; x++)
                {
                    char tile = line[x];
                    var position = new Vector2Int(x, y);

                    if (tile == '#')
                    {
                        cells.Add(position);
                        walls.Add(position);
                    }
                    else if (tile == '.' || tile == 'S' || tile == 'K' || tile == 'P')
                    {
                        cells.Add(position);
                    }
                    else if (tile == '_')
                    {
                        borders.Add(position);
                    }

                    if (tile == 'S')
                    {
                        level.playerStart = position;
                    }
                    else if (tile == 'K')
                    {
                        level.kitchenPosition = position;
                    }
                    else if (tile == 'P')
                    {
                        level.petStart = position;
                    }
                }
            }

            level.grid.width = width;
            level.grid.height = height;
            level.wallCells = walls.ToArray();
            level.mapCells = cells.ToArray();
            level.borderCells = borders.ToArray();
            EditorUtility.SetDirty(level);
        }
    }
}
