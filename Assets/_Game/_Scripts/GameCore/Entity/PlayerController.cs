using UnityEngine;

public class PlayerController : GridEntityController
{
    private Direction faceDirection;

    private bool isHoldingFood;

    private HoldFoodState holdFoodState;

    public override void Initialize(GridManager gridManager, Vector3Int gridPos)
    {
        base.Initialize(gridManager, gridPos);
        faceDirection = Direction.Down;
        isHoldingFood = false;

        holdFoodState = new HoldFoodState(this);
        stateMachine.AddTransition(idleState, holdFoodState, () => isHoldingFood);
        stateMachine.AddTransition(holdFoodState, idleState, () => !isHoldingFood);
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
        
        
        if (isHoldingFood)
        {
            if (interactedEntityController is IFeedable feedable)
            {
                feedable.OnFed();
                isHoldingFood = false;
            }
        }
        else
        {
            if (interactedEntityController is OvenController _)
            {
                isHoldingFood = true;
                Debug.Log("Picked up food from oven");
            }
            else
            {
                Debug.Log("No food to feed " + interactedEntityController.gameObject.name);
            }
        }
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

    public void SetHoldingFood(bool isHoldingFood) => this.isHoldingFood = isHoldingFood;
}
