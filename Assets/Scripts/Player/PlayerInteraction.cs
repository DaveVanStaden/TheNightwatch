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
            Ray ray = manager.playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, manager.interactionRange))
            {
                if (hit.collider.TryGetComponent<IInteraction>(out var interactable))
                {
                    activeInteraction = interactable;
                    activeInteraction.EnterInteraction(manager);
                    isInteracting = true;
                    manager.inInteractionView = true;
                    manager.currentLayer = 0;
                }
            }
        }

        // Update active interaction
        if (isInteracting && activeInteraction != null)
        {
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
    }
}
