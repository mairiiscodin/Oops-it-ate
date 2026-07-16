using UnityEngine;

namespace OopsItAte.Grid
{
    [CreateAssetMenu(
        fileName = "Grid Tile Theme",
        menuName = "Oops It Ate/Grid Tile Theme")]
    public sealed class GridTileTheme : ScriptableObject
    {
        [Header("Base Tiles")]
        public Sprite floor;
        public Sprite wall;

        [Header("Border Tile")]
        public Sprite borderIsolated;

        [Header("Border: connected in one direction")]
        public Sprite borderNorth;
        public Sprite borderEast;
        public Sprite borderSouth;
        public Sprite borderWest;

        [Header("Border: corners")]
        public Sprite borderNorthEast;
        public Sprite borderEastSouth;
        public Sprite borderSouthWest;
        public Sprite borderWestNorth;

        [Header("Border: opposite sides")]
        public Sprite borderNorthSouth;
        public Sprite borderEastWest;

        [Header("Border: connected in three directions")]
        public Sprite borderNorthEastSouth;
        public Sprite borderEastSouthWest;
        public Sprite borderNorthSouthWest;
        public Sprite borderNorthEastWest;

        [Header("Border: connected in four directions")]
        public Sprite borderAllSides;

        public Sprite GetBorderSprite(int connectedNeighborMask)
        {
            switch (connectedNeighborMask)
            {
                case 0: return borderIsolated;
                case 1: return borderNorth;
                case 2: return borderEast;
                case 4: return borderSouth;
                case 8: return borderWest;
                case 3: return borderNorthEast;
                case 6: return borderEastSouth;
                case 12: return borderSouthWest;
                case 9: return borderWestNorth;
                case 5: return borderNorthSouth;
                case 10: return borderEastWest;
                case 7: return borderNorthEastSouth;
                case 14: return borderEastSouthWest;
                case 13: return borderNorthSouthWest;
                case 11: return borderNorthEastWest;
                case 15: return borderAllSides;
                default: return null;
            }
        }

        public Sprite GetSingleSideSprite(int sideMask)
        {
            switch (sideMask)
            {
                case 1: return borderNorth;
                case 2: return borderEast;
                case 4: return borderSouth;
                case 8: return borderWest;
                default: return null;
            }
        }
    }
}
