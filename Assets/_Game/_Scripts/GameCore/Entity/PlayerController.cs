using UnityEngine;

public class PlayerController : GridEntityController
{
    private Direction faceDirection;

    private bool isHoldingFood;

    private void Awake()
    {
        faceDirection = Direction.Down;
    }
    
    private void Start()
    {
        GameInputManager.OnMovementPerformed -= GameInputManager_OnMovementPerformed;
        GameInputManager.OnInteractPerformed -= GameInputManager_OnInteractPerformed;
        
        GameInputManager.OnMovementPerformed += GameInputManager_OnMovementPerformed;
        GameInputManager.OnInteractPerformed += GameInputManager_OnInteractPerformed;
    }

    private void GameInputManager_OnInteractPerformed()
    {
        GridEntityController interactedEntityController;
        
        Vector3Int interactedCellPos = 
            gridPos + (Vector3Int)DirectionConverter.FromEnumToVector2Int(faceDirection);
        GridEntityController gridEntityController = 
            gridManager.GetEntityAtGridPos(interactedCellPos);
        
        if (!gridManager.IsGridPosOccupied(interactedCellPos) || 
            gridEntityController == null) interactedEntityController = this;
        else interactedEntityController = gridEntityController;
        
        interactedEntityController?.OnFed();
    }

    private void GameInputManager_OnMovementPerformed(Vector2Int direction)
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
        
        
        Vector3Int nextGridPos = gridManager.EntityTilemap.WorldToCell(transform.position + (Vector3Int)direction);
        if (CanMove(nextGridPos))
        {
            transform.position += (Vector3Int)direction;
            gridManager.SetGridPosOccupied(nextGridPos, true);
            gridManager.SetGridPosOccupied(gridPos, false);
            gridPos = nextGridPos;
        }
        
        faceDirection = DirectionConverter.FromVector2IntToEnum(direction);
    }

    private bool CanMove(Vector3Int pos) => !gridManager.IsGridPosOccupied(pos);
}
