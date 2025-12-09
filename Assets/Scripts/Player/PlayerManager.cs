using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

[RequireComponent(typeof(CharacterController))]
public class PlayerManager : MonoBehaviour
{
    // Expose settings in Inspector
    [Header("Movement Settings")]
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;

    [Header("Look Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public bool useHeadBob = true;

    [Header("Interaction Settings")]
    public float currentLayer = 0;
    public float interactionRange = 100f;

    [Header("Headbob Settings")]
    public float walkBobSpeed = 14f;
    public float walkBobAmount = .2f;
    public float runBobSpeed = 20f;
    public float runBobAmount = .4f;
    public float defaultYPos = 0f;
    public float bobStartupSpeed = 1f;
    public float bobRecoverySpeed = 1f;
    public float bobSprintRecoverySpeed = 1f;

    [Header("Footstep parameters")]
    public bool enableFootsteps = true;
    public float baseStepSpeed = 0.5f;
    public float runStepMultiplier = 1.5f;
    public AudioSource footstepAudioSource;
    public AudioClip[] footstepSound;

    [Header("Camera")]
    public Transform cameraParent; // Assign this in the Inspector
    public Camera rayCam;
    public Camera interactionCamera;
    public Transform cameraAnchor;
    public float smoothSpeed = 20f;

    [HideInInspector] public bool inInteractionView = false;
    [HideInInspector] public CharacterController characterController;
    [HideInInspector] public PlayerInputManager inputActions;
    // Set by interactions (like BreakerBox) when the interaction wants the player to leave.
    // PlayerInteraction will observe this flag and clean up interaction state properly.
    [HideInInspector] public bool externalLeaveRequested = false;

    private List<PlayerModule> modules = new();

    public PlayerCameraLook cameraLookModule;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Initialize inputActions ONCE here:
        inputActions = new PlayerInputManager();
        inputActions.Enable();

        // Add your modules here, passing 'this' as manager
        cameraLookModule = new PlayerCameraLook(this);
        modules.Add(cameraLookModule);
        modules.Add(new PlayerMovement(this));
        modules.Add(new PlayerInteraction(this));

        footstepAudioSource = GetComponent<AudioSource>();
        defaultYPos = playerCamera.transform.localPosition.y;
        foreach (var m in modules) m.OnAwake();
        DisableCameraForSeconds(0.1f); // avoid snap on start
    }

    void Start()
    {
        foreach (var m in modules) m.OnStart();
    }

    void Update()
    {
        foreach (var m in modules) m.OnUpdate();
    }

    /// <summary>
    /// Pause camera movement (for menus / pause). Safe no-op if module missing.
    /// Also unlocks cursor for UI interaction.
    /// </summary>
    public void PauseCamera()
    {
        if (cameraLookModule == null)
        {
            Debug.LogWarning("[PlayerManager] PauseCamera called but cameraLookModule is null.");
            return;
        }

        cameraLookModule.PauseCamera();
        // show cursor so UI can be used while paused
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[PlayerManager] Camera paused.");
    }

    /// <summary>
    /// Resume camera movement. Syncs yaw/pitch to camera to avoid snapping.
    /// Also locks and hides the cursor.
    /// </summary>
    public void ResumeCamera()
    {
        if (cameraLookModule == null)
        {
            Debug.LogWarning("[PlayerManager] ResumeCamera called but cameraLookModule is null.");
            return;
        }

        cameraLookModule.ResumeCamera();
        // restore cursor state for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("[PlayerManager] Camera resumed.");
    }

    /// <summary>
    /// Temporarily disable camera input for the given seconds (default 0.1s).
    /// Useful on startup or when closing menus to avoid snap.
    /// </summary>
    public void DisableCameraForSeconds(float seconds = 0.1f)
    {
        if (cameraLookModule == null)
        {
            Debug.LogWarning("[PlayerManager] DisableCameraForSeconds called but cameraLookModule is null.");
            return;
        }

        cameraLookModule.DisableForSeconds(seconds);
        Debug.Log($"[PlayerManager] Camera disabled for {seconds} seconds.");
    }
}
