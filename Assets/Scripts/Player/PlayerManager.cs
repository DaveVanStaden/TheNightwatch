using UnityEngine;
using System.Collections.Generic;

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

    [Header("Interaction Settings")]
    public float interactionRange = 100f;

    [Header("Camera")]
    public Transform cameraParent; // Assign this in the Inspector

    [HideInInspector] public bool inInteractionView = false;
    [HideInInspector] public CharacterController characterController;
    [HideInInspector] public PlayerInputManager inputActions;

    private List<PlayerModule> modules = new();

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // Initialize inputActions ONCE here:
        inputActions = new PlayerInputManager();
        inputActions.Enable();

        // Add your modules here, passing 'this' as manager
        modules.Add(new PlayerMovement(this));
        modules.Add(new PlayerCameraLook(this));
        modules.Add(new PlayerInteraction(this));

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
