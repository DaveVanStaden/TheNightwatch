using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : PlayerModule
{
    private PlayerInputManager inputActions;
    private Vector2 moveInput;
    private float velocityY;

    public PlayerMovement(PlayerManager manager) : base(manager)
    {
        inputActions = manager.inputActions; // Use shared instance
    }

    public override void OnUpdate()
    {
        if (manager.inInteractionView) return; // Prevent movement during interaction

        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        bool isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0.1f;

        // Directly calculate movement, no smoothing
        Vector3 targetMove = manager.transform.right * moveInput.x + manager.transform.forward * moveInput.y;
        targetMove *= isSprinting ? manager.runSpeed : manager.walkSpeed;

        // Gravity
        if (manager.characterController.isGrounded)
        {
            velocityY = -0.5f;
        }
        else
        {
            velocityY -= manager.gravity * Time.deltaTime;
        }

        Vector3 finalMove = new Vector3(targetMove.x, velocityY, targetMove.z);
        manager.characterController.Move(finalMove * Time.deltaTime);
    }
}