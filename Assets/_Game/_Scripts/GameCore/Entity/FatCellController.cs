using UnityEngine;

public class FatCellController : MonoBehaviour, IFeedable
{
    private GridEntityController gridEntityController;
    [SerializeField] private SpriteRenderer visualSpriteRenderer;
    private Vector3Int gridPos;

    public void OnFed()
    {
        gridEntityController.OnFed();
    }

    public void SetSprite(Sprite sprite)
    {
        visualSpriteRenderer.sprite = sprite;
    }

    public void SetGridPos(Vector3Int gridPos)
    {
        this.gridPos = gridPos;
    }
    public Vector3Int GetGridPos()
    {
        return gridPos;
    }
}
