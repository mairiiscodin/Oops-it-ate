using UnityEngine;

public class FatCellController : MonoBehaviour, IFeedable
{
    private GridEntityController gridEntityController;
    private Sprite sprite;
    private Vector3Int gridPos;

    public void OnFed()
    {
        gridEntityController.OnFed();
    }
}
