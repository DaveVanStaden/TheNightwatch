using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraLook : PlayerModule
{
    private PlayerInputManager inputActions;
    private Vector2 lookInput;
    private float rotationX = 0f;
    private float currentYaw = 0f;
    private float currentPitch = 0f;

    // Smoothing
    private float smoothSpeed = 12f; // Higher = snappier

    public PlayerCameraLook(PlayerManager manager) : base(manager)
    {
        inputActions = manager.inputActions; // Use shared instance
        if (manager.playerCamera != null)
        {
            rotationX = manager.playerCamera.transform.localEulerAngles.x;
            currentPitch = rotationX;
        }
    }

    public override void OnUpdate()
    {
        if (manager.inInteractionView || manager.playerCamera == null) return;

        // Read look input (mouse or right stick)
        lookInput = inputActions.Player.Look.ReadValue<Vector2>() * manager.lookSpeed;

        // Calculate target yaw and pitch
        currentYaw += lookInput.x;
        currentPitch -= lookInput.y;
        currentPitch = Mathf.Clamp(currentPitch, -manager.lookXLimit, manager.lookXLimit);

        // Smoothly interpolate camera pitch (up/down)
        float smoothPitch = Mathf.LerpAngle(
            manager.playerCamera.transform.localEulerAngles.x,
            currentPitch,
            Time.deltaTime * smoothSpeed
        );

        // Smoothly interpolate player yaw (left/right)
        float smoothYaw = Mathf.LerpAngle(
            manager.transform.eulerAngles.y,
            currentYaw,
            Time.deltaTime * smoothSpeed
        );

        // Apply rotations
        manager.playerCamera.transform.localRotation = Quaternion.Euler(smoothPitch, 0f, 0f);
        manager.transform.rotation = Quaternion.Euler(0f, smoothYaw, 0f);
    }
}
