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
        // Start interaction / pickup
        if (!isInteracting && inputActions.Player.Interact.triggered)
        {
            // Use the player's rayCam (or playerCamera) to start the interaction.
            Camera castCam = manager.rayCam != null ? manager.rayCam :
                              manager.playerCamera != null ? manager.playerCamera :
                              Camera.main;

            if (castCam == null)
            {
                Debug.LogWarning("[PlayerInteraction] No camera available for interaction raycast.");
                return;
            }

            // Decide how to create the ray:
            // - If the player is using the mouse and the cursor is unlocked, ray should come from the cursor (ScreenPointToRay).
            // - Otherwise (locked cursor / gamepad) cast from the camera's position along the camera's forward vector.
            bool hasMouse = Mouse.current != null;
            Ray ray;

            if (hasMouse && Cursor.lockState != CursorLockMode.Locked)
            {
                Vector2 screenPos = Mouse.current.position.ReadValue();
                ray = castCam.ScreenPointToRay(screenPos);
            }
            else
            {
                // Use the cast camera (the camera we're using for the raycast) as authoritative source
                // for both origin and forward. Previously we used manager.playerCamera here which could
                // differ from the camera actually used for raycasting (castCam), causing mismatch.
                Vector3 camForward;
                if (manager.cameraLookModule != null)
                {
                    camForward = manager.cameraLookModule.GetCameraForward();
                }
                else
                {
                    camForward = castCam.transform.forward;
                }

                // Start the ray at the cast camera near plane to avoid self-intersection
                Vector3 origin = castCam.transform.position + camForward.normalized * castCam.nearClipPlane;
                ray = new Ray(origin, camForward.normalized);
            }

            // Use configured interactionRange but ensure a sensible minimum
            float range = Mathf.Max(manager.interactionRange, 250f);

            Debug.Log($"[PlayerInteraction] Interact pressed. Camera={castCam.name}, cursorLocked={(Cursor.lockState==CursorLockMode.Locked)}, mousePresent={(Mouse.current!=null)}, rayOrigin={ray.origin}, rayDir={ray.direction}, range={range}");

            // draw debug ray for troubleshooting (visible in Scene view)
            Debug.DrawRay(ray.origin, ray.direction * range, Color.green, 1f);

            // include triggers so in-scene trigger colliders are detected
            if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.Log($"[PlayerInteraction] Ray hit: {hit.collider.name} (layer {hit.collider.gameObject.layer})");
                if (ProcessHit(hit)) return;
            }
            else
            {
                // Primary raycast missed: try more diagnostic / tolerant fallbacks.

                Debug.Log("[PlayerInteraction] Raycast missed. Running RaycastAll and SphereCast fallbacks.");

                // 1) RaycastAll fallback - logs what is along the ray (helps diagnose occluders)
                var allHits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
                if (allHits != null && allHits.Length > 0)
                {
                    // sort by distance
                    System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
                    Debug.Log($"[PlayerInteraction] RaycastAll returned {allHits.Length} hits - using nearest as fallback: {allHits[0].collider.name}");
                    if (ProcessHit(allHits[0])) return;
                }

                // 2) SphereCast fallback - tolerate slight aim offsets / small height differences
                float sphereRadius = 0.3f; // tweak if needed
                var sphereHits = Physics.SphereCastAll(ray, sphereRadius, range, ~0, QueryTriggerInteraction.Collide);
                if (sphereHits != null && sphereHits.Length > 0)
                {
                    System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
                    Debug.Log($"[PlayerInteraction] SphereCastAll returned {sphereHits.Length} hits (r={sphereRadius}) - using nearest: {sphereHits[0].collider.name}");
                    if (ProcessHit(sphereHits[0])) return;
                }

                // 3) OverlapSphere sample along the ray (useful if objects are slightly off the ray)
                int samples = 8;
                float step = range / samples;
                for (int i = 1; i <= samples; i++)
                {
                    Vector3 samplePoint = ray.origin + ray.direction * (i * step);
                    Collider[] overlaps = Physics.OverlapSphere(samplePoint, sphereRadius, ~0, QueryTriggerInteraction.Collide);
                    if (overlaps != null && overlaps.Length > 0)
                    {
                        // pick closest overlap to camera
                        Collider chosen = overlaps[0];
                        float bestDist = Vector3.Distance(ray.origin, chosen.transform.position);
                        for (int j = 1; j < overlaps.Length; j++)
                        {
                            float d = Vector3.Distance(ray.origin, overlaps[j].transform.position);
                            if (d < bestDist)
                            {
                                chosen = overlaps[j];
                                bestDist = d;
                            }
                        }
                        Debug.Log($"[PlayerInteraction] OverlapSphere found collider '{chosen.name}' near ray at sample {i}.");
                        // build a synthetic RaycastHit-like wrapper by doing a short ray towards the chosen collider
                        if (Physics.Raycast(ray, out RaycastHit overlapHit, range, ~0, QueryTriggerInteraction.Collide))
                        {
                            if (ProcessHit(overlapHit)) return;
                        }
                        else
                        {
                            // fallback: try to process collider directly
                            if (ProcessColliderDirect(chosen)) return;
                        }
                    }
                }

                Debug.Log("[PlayerInteraction] All fallbacks failed - nothing hit.");
            }
        }

        // Update active interaction
        if (isInteracting && activeInteraction != null)
        {
            // Allow global Escape to leave and ensure PlayerInteraction state is cleaned up
            if (Input.GetKey(KeyCode.LeftShift) && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                manager.StopAllCoroutines();
                StartLeaveInteraction();
                return;
            }

            // If an interaction requested an external leave (e.g. BreakerBox detected S),
            // honor it here so PlayerInteraction can run the proper leave flow / cleanup.
            if (manager.externalLeaveRequested)
            {
                manager.externalLeaveRequested = false;
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

    // Returns true if the hit was handled (key picked or interaction started)
    private bool ProcessHit(RaycastHit hit)
    {
        // New: central key pickup handling
        var key = hit.collider.GetComponentInParent<KeyItem>();
        if (key == null)
        {
            // also check children if KeyItem is on a child object
            key = hit.collider.GetComponentInChildren<KeyItem>();
        }

        if (key != null)
        {
            Debug.Log($"[PlayerInteraction] Picking up key '{key.keyId}' via raycast.");
            key.PickupBy(manager, "interaction");
            return true;
        }

        // Prefer a BreakerBox if present on the hit object or a parent (handles wrapper Interactable objects)
        BreakerBox breaker = hit.collider.GetComponentInParent<BreakerBox>();
        if (breaker == null)
        {
            // also check children if your breaker components live on child objects
            breaker = hit.collider.GetComponentInChildren<BreakerBox>();
        }

        if (breaker != null)
        {
            Debug.Log($"[PlayerInteraction] Found BreakerBox on '{breaker.gameObject.name}' � starting that interaction.");

            // mark interaction state early so other systems can react
            manager.inInteractionView = true;

            activeInteraction = breaker;
            // Unlock cursor and start interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            activeInteraction.EnterInteraction(manager);
            isInteracting = true;
            manager.currentLayer = 0;
            return true;
        }

        // Robust lookup for any IInteraction on hit collider, parents or children
        IInteraction interactable = null;

        // 1) try exact collider
        if (!hit.collider.TryGetComponent<IInteraction>(out interactable))
        {
            // 2) walk parents
            Transform t = hit.collider.transform.parent;
            while (t != null && interactable == null)
            {
                interactable = t.GetComponent<IInteraction>();
                t = t.parent;
            }

            // 3) fallback to children
            if (interactable == null)
            {
                interactable = hit.collider.GetComponentInChildren<IInteraction>();
            }
        }

        if (interactable != null)
        {
            Debug.Log($"[PlayerInteraction] Starting interaction with {interactable.GetType().Name}");
            // mark interaction state early
            manager.inInteractionView = true;

            activeInteraction = interactable;
            activeInteraction.EnterInteraction(manager);
            isInteracting = true;
            manager.currentLayer = 0;
            return true;
        }

        return false;
    }

    // Directly process a Collider from OverlapSphere fallback (returns true when handled)
    private bool ProcessColliderDirect(Collider col)
    {
        if (col == null) return false;

        // Key check
        var key = col.GetComponentInParent<KeyItem>() ?? col.GetComponentInChildren<KeyItem>();
        if (key != null)
        {
            Debug.Log($"[PlayerInteraction] (Overlap) Picking up key '{key.keyId}' via raycast fallback.");
            key.PickupBy(manager, "interaction_fallback");
            return true;
        }

        var breaker = col.GetComponentInParent<BreakerBox>() ?? col.GetComponentInChildren<BreakerBox>();
        if (breaker != null)
        {
            Debug.Log($"[PlayerInteraction] (Overlap) Starting BreakerBox '{breaker.gameObject.name}'.");
            manager.inInteractionView = true;
            activeInteraction = breaker;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            activeInteraction.EnterInteraction(manager);
            isInteracting = true;
            manager.currentLayer = 0;
            return true;
        }

        // other IInteraction on parent/children
        var interactable = col.GetComponent<IInteraction>() ?? col.GetComponentInParent<IInteraction>() ?? col.GetComponentInChildren<IInteraction>();
        if (interactable != null)
        {
            Debug.Log($"[PlayerInteraction] (Overlap) Starting interaction with {interactable.GetType().Name}.");
            manager.inInteractionView = true;
            activeInteraction = interactable;
            activeInteraction.EnterInteraction(manager);
            isInteracting = true;
            manager.currentLayer = 0;
            return true;
        }

        return false;
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
        var fl = Object.FindAnyObjectByType<Flashlight>();
        if (fl != null)
        {
            fl.EnableInput();
        }

        // Re-lock and hide the cursor when leaving interaction so normal player look resumes
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
