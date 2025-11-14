using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerInteraction : PlayerModule
{
    private PlayerInputManager inputActions;
    private IInteraction activeInteraction;
    private bool isInteracting = false;

    // Track previous move input for edge detection
    private Vector2 prevMoveInput = Vector2.zero;

    public PlayerInteraction(PlayerManager manager) : base(manager)
    {
        inputActions = manager.inputActions;
    }

    public override void OnUpdate()
    {
        // Start interaction
        if (!isInteracting && inputActions.Player.Interact.triggered)
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            // Use the player's rayCam (or playerCamera) to start the interaction.
            Camera castCam = manager.rayCam != null ? manager.rayCam :
                              manager.playerCamera != null ? manager.playerCamera :
                              Camera.main;

            if (castCam == null)
            {
                Debug.LogWarning("[PlayerInteraction] No camera available for interaction raycast.");
                return;
            }

            Ray ray = castCam.ScreenPointToRay(mousePos);
            Debug.Log($"[PlayerInteraction] Interact pressed. Using camera: {castCam.name}. Ray origin={ray.origin}, dir={ray.direction}, mouse={mousePos}");

            // include triggers so in-scene trigger colliders are detected
            if (Physics.Raycast(ray, out RaycastHit hit, manager.interactionRange, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.Log($"[PlayerInteraction] Ray hit: {hit.collider.name} (layer {hit.collider.gameObject.layer})");

                // Prefer a BreakerBox if present on the hit object or a parent (handles wrapper Interactable objects)
                BreakerBox breaker = hit.collider.GetComponentInParent<BreakerBox>();
                if (breaker == null)
                {
                    // also check children if your breaker components live on child objects
                    breaker = hit.collider.GetComponentInChildren<BreakerBox>();
                }

                if (breaker != null)
                {
                    Debug.Log($"[PlayerInteraction] Found BreakerBox on '{breaker.gameObject.name}' — starting that interaction.");

                    // mark interaction state early so other systems can react
                    manager.inInteractionView = true;

                    activeInteraction = breaker;
                    // Unlock cursor and start interaction
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    activeInteraction.EnterInteraction(manager);
                    isInteracting = true;
                    manager.currentLayer = 0;
                }
                // Replace the generic IInteraction branch (the else-if) with this version that does NOT touch the Flashlight.
                else if (hit.collider.TryGetComponent<IInteraction>(out var interactable))
                {
                    Debug.Log($"[PlayerInteraction] Starting interaction with {interactable.GetType().Name}");
                    // mark interaction state early
                    manager.inInteractionView = true;

                    // Unlock and show the mouse so the player can click buttons while in interaction
                    //Cursor.lockState = CursorLockMode.None;
                    //Cursor.visible = true;

                    activeInteraction = interactable;
                    activeInteraction.EnterInteraction(manager);
                    isInteracting = true;
                    manager.currentLayer = 0;
                }
                else
                {
                    Debug.Log("[PlayerInteraction] Hit object does not implement IInteraction.");
                }
            }
            else
            {
                Debug.Log("[PlayerInteraction] Raycast missed.");
            }
        }

        // Update active interaction
        if (isInteracting && activeInteraction != null)
        {
            // Allow global Escape to leave and ensure PlayerInteraction state is cleaned up
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                manager.StopAllCoroutines();
                StartLeaveInteraction();
                return;
            }

            // DEBUG: show we are about to call UpdateInteraction and what instance it is
            string actType = activeInteraction.GetType().Name;
            string mbName = (activeInteraction is MonoBehaviour mb) ? mb.gameObject.name : "n/a";
            //ADebug.Log($"[PlayerInteraction] Calling UpdateInteraction on {actType} (gameObject='{mbName}')");

            activeInteraction.UpdateInteraction(manager);

            // If the interaction is an Interactable, handle camera angles and leaving
            if (activeInteraction is Interactable interactable && !interactable.busy)
            {
                Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();

                // Detect rising edge for S/down (wasd or stick down)
                bool sPressed = moveInput.y < -0.5f && prevMoveInput.y >= -0.5f;
                bool wPressed = moveInput.y > 0.5f && prevMoveInput.y <= 0.5f;
                bool aPressed = moveInput.x < -0.5f && prevMoveInput.x >= -0.5f;
                bool dPressed = moveInput.x > 0.5f && prevMoveInput.x <= 0.5f;

                if (wPressed) // W/up
                {
                    manager.currentLayer = 1;
                    manager.StopAllCoroutines();
                    manager.StartCoroutine(interactable.SetAngle(1));
                }
                else if (aPressed) // A/left
                {
                    manager.currentLayer = 1;
                    manager.StopAllCoroutines();
                    manager.StartCoroutine(interactable.SetAngle(2));
                }
                else if (dPressed) // D/right
                {
                    manager.currentLayer = 1;
                    manager.StopAllCoroutines();
                    manager.StartCoroutine(interactable.SetAngle(3));
                }
                else if (sPressed)
                {
                    if (manager.currentLayer >= 1)
                    {
                        manager.StopAllCoroutines();
                        manager.StartCoroutine(interactable.SetAngle(0));
                        manager.currentLayer--;
                    }
                    else
                    {
                        // Leave interaction
                        manager.StopAllCoroutines();
                        StartLeaveInteraction();
                    }
                }

                prevMoveInput = moveInput;
            }
            else
            {
                prevMoveInput = Vector2.zero;
            }
        }
        else
        {
            prevMoveInput = Vector2.zero;
        }
    }

    private void StartLeaveInteraction()
    {
        if (activeInteraction is Interactable interactable)
        {
            manager.StartCoroutine(LeaveInteractionCoroutine(interactable));
        }
        else if (activeInteraction != null)
        {
            activeInteraction.LeaveInteraction(manager);
            CleanupInteraction();
        }
    }

    private IEnumerator LeaveInteractionCoroutine(Interactable interactable)
    {
        yield return manager.StartCoroutine(interactable.LeaveTheThing(manager));
        CleanupInteraction();
    }

    private void CleanupInteraction()
    {
        activeInteraction = null;
        isInteracting = false;
        manager.currentLayer = 0;
        manager.inInteractionView = false;

        // Re-enable flashlight input when leaving interaction
        var fl = Object.FindObjectOfType<Flashlight>();
        if (fl != null)
        {
            fl.EnableInput();
        }

        // Re-lock and hide the cursor when leaving interaction so normal player look resumes
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
