using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputManager : MonoBehaviour
{
    private GameInput gameInput;

    public static event Action<Vector2Int> OnMovementPerformed;
    public static event Action OnInteractPerformed;

    private void Awake()
    {
        gameInput = new GameInput();
    }

    private void OnEnable()
    {
        gameInput.Enable();
    }

    private void OnDisable()
    {
        gameInput.Dispose();
        gameInput.Disable();
    }

    private void Start()
    {
        gameInput.Player.Movement.performed -= MovementPerformed;
        gameInput.Player.Interact.performed -= InteractPerformed;
        
        gameInput.Player.Movement.performed += MovementPerformed;
        gameInput.Player.Interact.performed += InteractPerformed;
    }

    private void MovementPerformed(InputAction.CallbackContext ctx)
    {
        Vector2 movement = ctx.ReadValue<Vector2>();
        OnMovementPerformed?.Invoke(new  Vector2Int((int)movement.x, (int)movement.y));
    }
    
    private void InteractPerformed(InputAction.CallbackContext ctx)
    {
        OnInteractPerformed?.Invoke();
    }
}
