using System.Collections.Generic;
using UnityEngine;

public class GridEntityController : MonoBehaviour, IFeedable
{
    protected GridManager gridManager;
    protected Vector3Int gridPos;
    protected bool isFed;
    protected Stack<(int fullness, Vector3Int gridPos)> occupiedGridPosStack;
    protected int currentFullness;
    protected int maxFullness;

    protected StateMachine stateMachine;
    protected IdleState idleState;

    

    public virtual void Initialize(GridManager gridManager, Vector3Int gridPos)
    {
        this.gridManager = gridManager;
        this.gridPos = gridPos;

        isFed = false;
        occupiedGridPosStack = new Stack<(int fullness, Vector3Int gridPos)>();
        currentFullness = 0;
        maxFullness = 3;
        occupiedGridPosStack.Push((0, gridPos));

        stateMachine = new StateMachine();
        idleState = new IdleState(this);
        stateMachine.AddAnyTransition(idleState, () => !isFed);
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
