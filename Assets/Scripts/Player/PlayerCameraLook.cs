using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraLook : PlayerModule
{
    private PlayerInputManager inputActions;
    private Vector2 lookInput;
    private float rotationX = 0f;
    private float currentYaw = 0f;
    private float currentPitch = 0f;
    private float yawBeforeInteraction = 0f;
    // Smoothing
    private float smoothSpeed = 0f; // Higher = snappier

    public PlayerCameraLook(PlayerManager manager) : base(manager)
    {
        smoothSpeed = manager.smoothSpeed;
        inputActions = manager.inputActions; // Use shared instance
        if (manager.playerCamera != null)
        {
            rotationX = manager.playerCamera.transform.localEulerAngles.x;
            currentPitch = rotationX;
        }
    }

    public override void OnUpdate()
    {
        if (manager.inInteractionView || manager.playerCamera == null)
            return;

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

    public void SetYawFromTransform()
    {
        // Use the current world yaw of the player/camera
        if (manager.playerCamera != null)
        {
            // Use the world Y rotation of the camera's transform
            currentYaw = manager.playerCamera.transform.eulerAngles.y;
        }
    }

    // After re-parenting and resetting the camera
    public void StoreYawBeforeInteraction()
    {
        if (manager.playerCamera != null)
            yawBeforeInteraction = manager.playerCamera.transform.eulerAngles.y;
    }

    public void RestoreYawAfterInteraction()
    {
        currentYaw = yawBeforeInteraction;
        // Also set the player and camera parent yaw
        if (manager.transform != null)
            manager.transform.rotation = Quaternion.Euler(0f, yawBeforeInteraction, 0f);
        if (manager.cameraParent != null)
        {
            Vector3 parentEuler = manager.cameraParent.eulerAngles;
            manager.cameraParent.rotation = Quaternion.Euler(parentEuler.x, yawBeforeInteraction, parentEuler.z);
        }
    }
    public void SyncYawToCamera()
    {
        if (manager.playerCamera != null)
        {
            float yaw = manager.playerCamera.transform.eulerAngles.y;
            currentYaw = yaw;
            manager.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (manager.cameraParent != null)
            {
                Vector3 parentEuler = manager.cameraParent.eulerAngles;
                manager.cameraParent.rotation = Quaternion.Euler(parentEuler.x, yaw, parentEuler.z);
            }
        }
    }

    public Vector3 GetCameraForward()
    {
        if (manager.playerCamera != null)
            return manager.playerCamera.transform.forward;
        return Vector3.forward;
    }
}
