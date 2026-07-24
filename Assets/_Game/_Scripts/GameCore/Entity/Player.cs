using UnityEngine;

public class Player : GridEntity
{
    private void Start()
    {
        GameInputManager.OnMovementPerformed += GameInputManager_OnMovementPerformed;
    }

    private void GameInputManager_OnMovementPerformed(Vector2 direction)
    {
        if (gridManager == null)
        {
            Debug.LogWarning("Can't find grid manager");
            return;
        }

        if (gridManager.EntityTilemap == null)
        {
            Debug.LogWarning("Can't find entity tilemap");
            return;
        }
        Vector3Int nextGridPos = gridManager.EntityTilemap.WorldToCell(transform.position + (Vector3)direction);
        if (CanMove(nextGridPos))
        {
            transform.position += (Vector3)direction;
            gridManager.SetGridPosOccupied(nextGridPos, true);
            gridManager.SetGridPosOccupied(gridPos, false);
            gridPos = nextGridPos;
        }
    }

    private bool CanMove(Vector3Int pos) => !gridManager.IsGridPosOccupied(pos);
}
