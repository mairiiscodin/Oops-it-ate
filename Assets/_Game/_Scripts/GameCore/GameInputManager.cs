using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputManager : MonoBehaviour
{
    private GameInput gameInput;

    public static event Action<Vector2> OnMovementPerformed;

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
        gameInput.Player.Movement.performed += MovementPerformed;
    }

    private void MovementPerformed(InputAction.CallbackContext ctx)
    {
        OnMovementPerformed?.Invoke(ctx.ReadValue<Vector2>());
    }
}
