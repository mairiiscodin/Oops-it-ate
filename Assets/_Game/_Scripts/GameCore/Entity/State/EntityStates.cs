public class IdleState : IState
{
    private GridEntityController gridEntityController;

    public IdleState(GridEntityController gridEntityController)
    {
        this.gridEntityController = gridEntityController;
    }

    public void Enter() {}

    public void FixedTick() {}

    public void Tick() {}

    public void Exit() {}
}

public class FedState : IState
{
    private GridEntityController gridEntityController;

    public FedState(GridEntityController gridEntityController)
    {
        this.gridEntityController = gridEntityController;
    }

    public void Enter()
    {
        gridEntityController.OnFed();
    }

    public void FixedTick() {}

    public void Tick() {}

    public void Exit() {}
}

public class HoldFoodState : IState
{
    private PlayerController playerController;

    public HoldFoodState(PlayerController playerController)
    {
        this.playerController = playerController;
    }

    public void Enter()
    {

    }

    public void FixedTick() {}

    public void Tick() {}

    public void Exit()
    {

    }
}
