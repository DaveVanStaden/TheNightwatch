using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerInteraction : PlayerModule
{
    private PlayerInputManager inputActions;
    private Interactable lastInteraction;
    private int currentLayer = 0;
    private bool isInteracting = false;

    // Track previous move input for edge detection
    private Vector2 prevMoveInput = Vector2.zero;

    public PlayerInteraction(PlayerManager manager) : base(manager)
    {
        inputActions = manager.inputActions; // Use shared instance
    }

    public override void OnUpdate()
    {
        // Start interaction
        if (!isInteracting && inputActions.Player.Interact.triggered)
        {
            Ray ray = manager.playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, manager.interactionRange))
            {
                if (hit.collider.TryGetComponent<Interactable>(out var interactable))
                {
                    lastInteraction = interactable;
                    lastInteraction.DoTheThing();
                    isInteracting = true;
                    manager.inInteractionView = true; // Disable camera look and movement
                    currentLayer = 0;
                }
            }
        }

        // Handle interaction camera view logic
        if (isInteracting && lastInteraction != null && !lastInteraction.busy)
        {
            Vector2 moveInput = inputActions.Player.Move.ReadValue<Vector2>();

            // Detect rising edge for S/down (wasd or stick down)
            bool sPressed = moveInput.y < -0.5f && prevMoveInput.y >= -0.5f;
            bool wPressed = moveInput.y > 0.5f && prevMoveInput.y <= 0.5f;
            bool aPressed = moveInput.x < -0.5f && prevMoveInput.x >= -0.5f;
            bool dPressed = moveInput.x > 0.5f && prevMoveInput.x <= 0.5f;

            if (wPressed) // W/up
            {
                currentLayer = 1;
                manager.StopAllCoroutines();
                manager.StartCoroutine(lastInteraction.SetAngle(1));
            }
            else if (aPressed) // A/left
            {
                currentLayer = 1;
                manager.StopAllCoroutines();
                manager.StartCoroutine(lastInteraction.SetAngle(2));
            }
            else if (dPressed) // D/right
            {
                currentLayer = 1;
                manager.StopAllCoroutines();
                manager.StartCoroutine(lastInteraction.SetAngle(3));
            }
            else if (sPressed)
            {
                if (currentLayer >= 1)
                {
                    manager.StopAllCoroutines();
                    manager.StartCoroutine(lastInteraction.SetAngle(0));
                    currentLayer--;
                }
                else
                {
                    // First S press: return camera to parent, and IMMEDIATELY allow player to move/look again
                    if (manager.cameraParent != null && manager.playerCamera != null)
                    {
                        manager.playerCamera.transform.SetParent(manager.cameraParent);
                        manager.playerCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    }
                    // Start the interactable's leave coroutine for proper cleanup (flashlight, etc.)
                    manager.StopAllCoroutines();
                    manager.StartCoroutine(LeaveInteraction());
                }
            }

            // Store current move input for next frame
            prevMoveInput = moveInput;
        }
        else
        {
            // Reset previous input if not interacting
            prevMoveInput = Vector2.zero;
        }
    }

    private IEnumerator LeaveInteraction()
    {
        if (lastInteraction != null)
        {
            yield return manager.StartCoroutine(lastInteraction.LeaveTheThing());
            lastInteraction = null;
        }
        isInteracting = false;
        currentLayer = 0;

        // Optionally reset camera again to parent (safety)
        if (manager.cameraParent != null && manager.playerCamera != null)
        {
            manager.playerCamera.transform.SetPositionAndRotation(
                manager.cameraParent.position,
                manager.cameraParent.rotation
            );
        }

        manager.inInteractionView = false; // Re-enable camera look and movement at the very end
    }
}
