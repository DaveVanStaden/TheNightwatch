using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : PlayerModule
{
    private PlayerInputManager inputActions;
    private Vector2 moveInput;
    private float velocityY;
    private bool isSprinting;

    private float footstepTimer = 0f;
    private float headbobTimer;
    float bobIntensity = 0f;

    private float GetCurrentOffset => isSprinting ? manager.baseStepSpeed * manager.runStepMultiplier : manager.baseStepSpeed;

    public PlayerMovement(PlayerManager manager) : base(manager)
    {
        inputActions = manager.inputActions; // Use shared instance
    }

    public override void OnUpdate()
    {
        if (manager.inInteractionView) return; // Prevent movement during interaction

        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0.1f;

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

        if (manager.enableFootsteps)
        {
            HandleFootsteps();
        }
        if (manager.useHeadBob)
        {
            HandleHeadbob();
        }
    }
    private void HandleFootsteps()
    {
        if (!manager.characterController.isGrounded) return;
        if (moveInput == Vector2.zero) return;

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0)
        {
            manager.footstepAudioSource.pitch = Random.Range(0.9f, 1.1f);
            manager.footstepAudioSource.PlayOneShot(manager.footstepSound[Random.Range(0, manager.footstepSound.Length)]);
            footstepTimer = GetCurrentOffset;
        }
    }
    private void HandleHeadbob()
    {
        headbobTimer += Time.deltaTime * (isSprinting ? manager.runBobSpeed : manager.walkBobSpeed);
        if (Mathf.Abs(moveInput.x) > 0.1f || Mathf.Abs(moveInput.y) > 0.1f)
        {
            bobIntensity += manager.bobStartupSpeed * Time.deltaTime;
        }
        else
        {
            bobIntensity -= (isSprinting ? manager.bobSprintRecoverySpeed : manager.bobRecoverySpeed) * Time.deltaTime;
        }

        bobIntensity = Mathf.Clamp(bobIntensity, 0f, 1f);
        manager.playerCamera.transform.localPosition = new Vector3(
            manager.playerCamera.transform.localPosition.x,
            manager.defaultYPos + Mathf.Sin(headbobTimer) * (isSprinting ? manager.runBobAmount : manager.walkBobAmount) * bobIntensity,
            manager.playerCamera.transform.localPosition.z);
    }
}