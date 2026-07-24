using UnityEngine;

public class GridEntity : MonoBehaviour
{
    protected GridManager gridManager;
    protected Vector3Int gridPos;

    public void Initialize(GridManager gridManager, Vector3Int gridPos)
    {
        this.gridManager = gridManager;
        this.gridPos = gridPos;
    }

    public Vector3Int GetGridPos() => gridPos;
    public void SetGridPos(Vector3Int gridPos)
    {
        this.gridPos = gridPos;
    }
}
