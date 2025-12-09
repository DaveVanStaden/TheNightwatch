using System.Collections;
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

    // Camera enable flag (controls whether camera input is processed)
    private bool cameraEnabled = true;

    public PlayerCameraLook(PlayerManager manager) : base(manager)
    {
        smoothSpeed = manager.smoothSpeed;
        inputActions = manager.inputActions; // Use shared instance
        if (manager.playerCamera != null)
        {
            rotationX = manager.playerCamera.transform.localEulerAngles.x;
            currentPitch = SignedAngleFrom0To180(rotationX);
            // initialize yaw/pitch from camera so first frames don't snap
            currentYaw = SignedAngleFrom0To360(manager.playerCamera.transform.eulerAngles.y);
            currentPitch = SignedAngleFrom0To180(manager.playerCamera.transform.localEulerAngles.x);
        }
    }

    public override void OnUpdate()
    {
        if (!cameraEnabled || manager.inInteractionView || manager.playerCamera == null)
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
            currentYaw = SignedAngleFrom0To360(manager.playerCamera.transform.eulerAngles.y);
        }
    }

    // After re-parenting and resetting the camera
    public void StoreYawBeforeInteraction()
    {
        if (manager.playerCamera != null)
            yawBeforeInteraction = SignedAngleFrom0To360(manager.playerCamera.transform.eulerAngles.y);
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
            float yaw = SignedAngleFrom0To360(manager.playerCamera.transform.eulerAngles.y);
            currentYaw = yaw;
            // Immediately align player transform to avoid snap when resuming
            manager.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (manager.cameraParent != null)
            {
                Vector3 parentEuler = manager.cameraParent.eulerAngles;
                manager.cameraParent.rotation = Quaternion.Euler(parentEuler.x, yaw, parentEuler.z);
            }

            // also sync pitch so camera doesn't snap vertically (normalize to signed -180..180 then clamp)
            float rawLocalPitch = manager.playerCamera.transform.localEulerAngles.x;
            float signedPitch = SignedAngleFrom0To180(rawLocalPitch);
            currentPitch = Mathf.Clamp(signedPitch, -manager.lookXLimit, manager.lookXLimit);
        }
    }

    /// <summary>
    /// Public API: pause camera movement (menus, pause etc.)
    /// Call ResumeCamera() to re-enable. Pausing preserves current yaw/pitch.
    /// </summary>
    public void PauseCamera()
    {
        cameraEnabled = false;
    }

    /// <summary>
    /// Public API: resume camera movement and sync to current camera transform to avoid snapping.
    /// </summary>
    public void ResumeCamera()
    {
        // sync yaw/pitch so the first frame after resume does not snap
        SyncYawToCamera();
        cameraEnabled = true;
    }

    /// <summary>
    /// Disable camera input for a short period (seconds). Useful on startup or when closing menus
    /// to avoid immediate snap when camera/game resumes.
    /// </summary>
    public void DisableForSeconds(float seconds = 0.1f)
    {
        if (manager != null)
            manager.StartCoroutine(DisableCoroutine(seconds));
        else
            StartDisableFallback(seconds);
    }

    // fallback if manager is null (shouldn't happen normally)
    private void StartDisableFallback(float seconds)
    {
        cameraEnabled = false;
        // best-effort re-enable after delay using Unity time (only usable in editor when manager null)
        // (No coroutine available here, keep simple: use Invoke if MonoBehaviour available)
    }

    private IEnumerator DisableCoroutine(float seconds)
    {
        cameraEnabled = false;
        yield return new WaitForSeconds(seconds);
        // sync to camera transform before enabling to avoid snap
        SyncYawToCamera();
        cameraEnabled = true;
    }

    public Vector3 GetCameraForward()
    {
        if (manager.playerCamera != null)
            return manager.playerCamera.transform.forward;
        return Vector3.forward;
    }

    // Helpers: convert Unity's 0..360 euler to signed angle ranges
    private static float SignedAngleFrom0To360(float angle)
    {
        // return angle as signed -180..+180 but for yaw we want 0..360 mapped to -180..180 space
        float a = angle;
        if (a > 180f) a -= 360f;
        return a;
    }

    private static float SignedAngleFrom0To180(float angle)
    {
        // local euler.x is 0..360; convert to -180..180 where forward/down semantics make sense for pitch
        float a = angle;
        if (a > 180f) a -= 360f;
        return a;
    }
}
