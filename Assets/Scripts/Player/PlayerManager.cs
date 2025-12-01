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
    [SerializeField] public float walkBobSpeed = 14f;
    [SerializeField] public float walkBobAmount = .2f;
    [SerializeField] public float runBobSpeed = 20f;
    [SerializeField] public float runBobAmount = .4f;
    public float defaultYPos = 0f;

    [Header("Footstep parameters")]
    public bool enableFootsteps = true;
    [SerializeField] public float baseStepSpeed = 0.5f;
    [SerializeField] public float runStepMultiplier = 1.5f;
    [SerializeField] public AudioSource footstepAudioSource;
    [SerializeField] public AudioClip[] footstepSound;

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
    }

    void Start()
    {
        foreach (var m in modules) m.OnStart();
    }

    void Update()
    {
        foreach (var m in modules) m.OnUpdate();
    }
}
