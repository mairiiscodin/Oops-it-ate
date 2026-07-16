using OopsItAte.Grid;
using System;
using UnityEngine;

namespace OopsItAte.Levels
{
    [CreateAssetMenu(menuName = "Oops It Ate/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public GridSettings grid = new GridSettings();
        public GridTileTheme tileTheme;
        public Vector2Int playerStart = new Vector2Int(1, 1);
        public Vector2Int kitchenPosition = new Vector2Int(2, 1);
        public Vector2Int petStart = new Vector2Int(4, 1);
        public Vector2Int[] wallCells = Array.Empty<Vector2Int>();
        public Vector2Int[] mapCells = Array.Empty<Vector2Int>();
        public Vector2Int[] borderCells = Array.Empty<Vector2Int>();

        [TextArea(6, 16)]
        public string textMap =
            "#######\n" +
            "#.....#\n" +
            "#.....#\n" +
            "#.....#\n" +
            "#.K.P.#\n" +
            "#.S...#\n" +
            "#######";
    }
}
