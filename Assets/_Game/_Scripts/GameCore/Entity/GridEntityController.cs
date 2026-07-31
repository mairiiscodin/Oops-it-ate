using System.Collections.Generic;
using UnityEngine;

public class GridEntityController : MonoBehaviour, IFeedable
{
    protected GridManager gridManager;
    protected Vector3Int gridPos;
    
    protected bool hasBeenFed;
    protected Stack<(int fullness, FatCellController fatCell)> fatCellStack;
    protected int currentFullness;
    protected int maxFullness;
    [SerializeField] protected Sprite fatSprite;

    protected StateMachine stateMachine;
    protected IdleState idleState;

    

    public virtual void Initialize(GridManager gridManager, Vector3Int gridPos)
    {
        this.gridManager = gridManager;
        this.gridPos = gridPos;

        hasBeenFed = false;
        fatCellStack = new Stack<(int fullness, FatCellController fatCell)>();
        currentFullness = 0;
        maxFullness = 3;
        fatCellStack.Push((0, null));

        stateMachine = new StateMachine();
        idleState = new IdleState(this);
        stateMachine.AddAnyTransition(idleState, () => !hasBeenFed);
    }

    public Vector3Int GetGridPos() => gridPos;
    public void SetGridPos(Vector3Int gridPos)
    {
        this.gridPos = gridPos;
    }
    
    public virtual void OnFed()
    {
        Debug.Log(gameObject.name + " fed.");
    }
}
