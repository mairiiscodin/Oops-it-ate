using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Entity Tile", menuName = "Custom Tiles/Entity Tile")]
public class TileEntity : TileBase
{
    public TileEntityType type;
    public GameObject prefab;
}
