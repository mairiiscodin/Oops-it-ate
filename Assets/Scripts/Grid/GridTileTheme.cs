using UnityEngine;
using UnityEngine.Serialization;

namespace OopsItAte.Grid
{
    [CreateAssetMenu(
        fileName = "Grid Tile Theme",
        menuName = "Oops It Ate/Grid Tile Theme")]
    public sealed class GridTileTheme : ScriptableObject
    {
        [Header("Base Tiles")]
        public Sprite floor;

        [Header("Walls")]
        [Tooltip("Used when the cell directly south is not another authored wall.")]
        public Sprite wallSouthOpen;

        [FormerlySerializedAs("wall")]
        [Tooltip("Used when the cell directly south is another authored wall.")]
        public Sprite wallSouthClosed;

        [Header("Directional Doors")]
        public Sprite doorUp;
        public Sprite doorDown;
        public Sprite doorLeft;
        public Sprite doorRight;

        [Header("Borders")]
        [Tooltip("Used when the cell directly south is not another border.")]
        public Sprite borderSouthOpen;

        [FormerlySerializedAs("borderIsolated")]
        [Tooltip("Used when the cell directly south is another border.")]
        public Sprite borderSouthClosed;

        public Sprite GetWallSprite(bool isSouthOpen)
        {
            Sprite preferred = isSouthOpen ? wallSouthOpen : wallSouthClosed;
            return preferred != null
                ? preferred
                : (isSouthOpen ? wallSouthClosed : wallSouthOpen);
        }

        public Sprite GetBorderSprite(bool isSouthOpen)
        {
            Sprite preferred = isSouthOpen ? borderSouthOpen : borderSouthClosed;
            return preferred != null
                ? preferred
                : (isSouthOpen ? borderSouthClosed : borderSouthOpen);
        }
    }
}
