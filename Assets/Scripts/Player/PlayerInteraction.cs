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
            // Choose the camera used for the raycast
            Camera castCam = manager.playerCamera != null ? manager.playerCamera :
                              manager.rayCam != null ? manager.rayCam :
                              Camera.main;

            if (castCam == null)
            {
                Debug.LogWarning("[PlayerInteraction] No camera available for interaction raycast.");
                return;
            }

            // Build ray more robustly:
            // - When using the mouse & cursor unlocked use ScreenPointToRay (clamped to camera viewport)
            // - Otherwise cast from the camera using a forward computed from the look module (if any)
            bool hasMouse = Mouse.current != null;
            Ray ray;

            if (hasMouse && Cursor.lockState != CursorLockMode.Locked)
            {
                Vector2 screenPos = Mouse.current.position.ReadValue();

                // Clamp screenPos to camera viewport to avoid weird off-screen coordinates when using other cameras
                screenPos.x = Mathf.Clamp(screenPos.x, 0f, castCam.pixelWidth - 1f);
                screenPos.y = Mathf.Clamp(screenPos.y, 0f, castCam.pixelHeight - 1f);

                ray = castCam.ScreenPointToRay(screenPos);
            }
            else
            {
                // Use camera transform for origin offset but allow the look module to provide direction
                Vector3 origin = castCam.transform.position + castCam.transform.forward * castCam.nearClipPlane;

                Vector3 forward = castCam.transform.forward;
                if (manager.cameraLookModule != null)
                {
                    // Use cameraLookModule primarily for direction but keep origin based on the camera transform
                    forward = manager.cameraLookModule.GetCameraForward();
                }

                ray = new Ray(origin, forward.normalized);
            }

            // Use configured range but keep a sensible min/max so we don't accidentally make it extremely tiny or huge
            float range = Mathf.Clamp(manager.interactionRange, 2f, 500f);

            Debug.Log($"[PlayerInteraction] Interact pressed. Camera={castCam.name}, cursorLocked={(Cursor.lockState==CursorLockMode.Locked)}, mousePresent={(Mouse.current!=null)}, rayOrigin={ray.origin}, rayDir={ray.direction}, range={range}");

            Debug.DrawRay(ray.origin, ray.direction * range, Color.green, 1f);

            // Primary raycast (include triggers)
            if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.Log($"[PlayerInteraction] Ray hit: {hit.collider.name} (layer {hit.collider.gameObject.layer}) distance={hit.distance}");
                if (ProcessHit(hit)) return;

                // EXTRA: if ray hit something but ProcessHit didn't start an interaction,
                // do a small proximity scan around the hit point (3D and 2D) to catch interactables
                // whose colliders are separate / 2D or offset from the visible object.
                if (ProximityScanForInteractions(hit.point))
                    return;
            }
            else
            {
                Debug.Log("[PlayerInteraction] Primary raycast missed. Running prioritized fallbacks.");

                // 1) RaycastAll fallback - iterate through sorted hits until one is handled
                var allHits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
                if (allHits != null && allHits.Length > 0)
                {
                    System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));
                    Debug.Log($"[PlayerInteraction] RaycastAll returned {allHits.Length} hits - iterating for actionable hit.");
                    foreach (var h in allHits)
                    {
                        Debug.Log($"[PlayerInteraction] RaycastAll candidate: {h.collider.name} @ {h.distance}");
                        if (ProcessHit(h)) return;
                    }
                }

                // 2) SphereCastAll fallback - iterate sorted hits
                float sphereRadius = Mathf.Max(0.001f, manager.interactionSphereRadius);
                var sphereHits = Physics.SphereCastAll(ray, sphereRadius, range, ~0, QueryTriggerInteraction.Collide);
                if (sphereHits != null && sphereHits.Length > 0)
                {
                    System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
                    Debug.Log($"[PlayerInteraction] SphereCastAll returned {sphereHits.Length} hits (r={sphereRadius}) - iterating for actionable hit.");
                    foreach (var sh in sphereHits)
                    {
                        Debug.Log($"[PlayerInteraction] SphereCast candidate: {sh.collider.name} @ {sh.distance}");
                        if (ProcessHit(sh)) return;
                    }
                }

                // 3) OverlapSphere samples along the ray (useful for objects slightly off the ray)
                int samples = 8;
                float step = range / samples;
                for (int i = 1; i <= samples; i++)
                {   
                    Vector3 samplePoint = ray.origin + ray.direction * (i * step);
                    Collider[] overlaps = Physics.OverlapSphere(samplePoint, sphereRadius, ~0, QueryTriggerInteraction.Collide);
                    if (overlaps != null && overlaps.Length > 0)
                    {
                        // Choose the closest overlap to the camera origin
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

                        Debug.Log($"[PlayerInteraction] OverlapSphere found collider '{chosen.name}' near ray at sample {i}. Trying targeted ray to that collider.");

                        // Ray towards the chosen collider center (more reliable than reusing the original direction)
                        Vector3 dirToChosen = (chosen.transform.position - ray.origin).normalized;
                        if (Physics.Raycast(ray.origin, dirToChosen, out RaycastHit overlapHit, range, ~0, QueryTriggerInteraction.Collide))
                        {
                            Debug.Log($"[PlayerInteraction] Targeted ray hit: {overlapHit.collider.name}");
                            if (ProcessHit(overlapHit)) return;
                        }
                        else
                        {
                            // Fallback: try to process the collider directly
                            if (ProcessColliderDirect(chosen)) return;
                        }
                    }
                }

                Debug.Log("[PlayerInteraction] All fallbacks failed - nothing handled the raycast.");
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
        GrabPhone phone = hit.collider.GetComponent<GrabPhone>();
        if (phone == null)
        {
            phone = hit.collider.GetComponentInChildren<GrabPhone>();
        }
        if (phone != null)
        {
            phone.ToFace();
            phone = null;
        }

        // Robust lookup for any IInteraction on hit collider, parents or children
        IInteraction interactable = null;

        // 1) search components on the exact gameObject and cast to IInteraction if any MonoBehaviour implements it
        interactable = FindIInteractionOnGameObject(hit.collider.gameObject);

        // 2) walk parents if not found
        Transform t = hit.collider.transform.parent;
        while (t != null && interactable == null)
        {
            interactable = FindIInteractionOnGameObject(t.gameObject);
            t = t.parent;
        }

        // 3) fallback to children if still not found
        if (interactable == null)
        {
            foreach (var mb in hit.collider.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is IInteraction ii)
                {
                    interactable = ii;
                    break;
                }
            }
        }

        if (interactable != null)
        {
            Debug.Log($"[PlayerInteraction] Starting interaction with {interactable.GetType().Name} (found via robust lookup)");
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

        // other IInteraction on parent/children — use robust lookup (components that implement IInteraction)
        IInteraction interactable = null;

        interactable = FindIInteractionOnGameObject(col.gameObject);

        if (interactable == null)
        {
            // parents
            Transform t = col.transform.parent;
            while (t != null && interactable == null)
            {
                interactable = FindIInteractionOnGameObject(t.gameObject);
                t = t.parent;
            }
        }

        if (interactable == null)
        {
            foreach (var mb in col.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is IInteraction ii)
                {
                    interactable = ii;
                    break;
                }
            }
        }

        if (interactable != null)
        {
            Debug.Log($"[PlayerInteraction] (Overlap) Starting interaction with {interactable.GetType().Name} (robust lookup).");
            manager.inInteractionView = true;
            activeInteraction = interactable;
            activeInteraction.EnterInteraction(manager);
            isInteracting = true;
            manager.currentLayer = 0;
            return true;
        }

        return false;
    }

    // Helper: find a component on the GameObject that implements IInteraction
    private IInteraction FindIInteractionOnGameObject(GameObject go)
    {
        if (go == null) return null;

        // Get all MonoBehaviours (fast and safe) and return the first that implements IInteraction
        var monos = go.GetComponents<MonoBehaviour>();
        if (monos == null || monos.Length == 0) return null;
        foreach (var mb in monos)
        {
            if (mb is IInteraction ii)
                return ii;
        }
        return null;
    }

    private void StartLeaveInteraction()
    {
        if (activeInteraction is Interactable interactable)
        {
            // Start the leave coroutine on the Interactable so manager.StopAllCoroutines()
            // (called elsewhere when switching angles) cannot cancel the leave coroutine.
            interactable.StartCoroutine(LeaveInteractionCoroutine(interactable));
        }
        else if (activeInteraction != null)
        {
            activeInteraction.LeaveInteraction(manager);
            CleanupInteraction();
        }
    }

    private IEnumerator LeaveInteractionCoroutine(Interactable interactable)
    {
        // Run the Interactable's LeaveTheThing coroutine on the Interactable itself
        // so it won't be killed by manager.StopAllCoroutines().
        yield return interactable.StartCoroutine(interactable.LeaveTheThing(manager));
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

    // EXTRA: proximity scan for interactions at a specific point (e.g. ray hit point)
    private bool ProximityScanForInteractions(Vector3 point)
    {
        // Sphere overlap check at the point of interest
        float sphereRadius = Mathf.Max(0.001f, manager.interactionSphereRadius);
        Collider[] nearbyColliders = Physics.OverlapSphere(point, sphereRadius, ~0, QueryTriggerInteraction.Collide);

        Debug.Log($"[PlayerInteraction] Proximity scan found {nearbyColliders.Length} colliders near point {point} (r={sphereRadius})");

        foreach (var col in nearbyColliders)
        {
            // Key check
            var key = col.GetComponentInParent<KeyItem>() ?? col.GetComponentInChildren<KeyItem>();
            if (key != null)
            {
                Debug.Log($"[PlayerInteraction] (Proximity) Picking up key '{key.keyId}'.");
                key.PickupBy(manager, "proximity_scan");
                return true;
            }

            var breaker = col.GetComponentInParent<BreakerBox>() ?? col.GetComponentInChildren<BreakerBox>();
            if (breaker != null)
            {
                Debug.Log($"[PlayerInteraction] (Proximity) Starting BreakerBox '{breaker.gameObject.name}'.");
                manager.inInteractionView = true;
                activeInteraction = breaker;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                activeInteraction.EnterInteraction(manager);
                isInteracting = true;
                manager.currentLayer = 0;
                return true;
            }

            // other IInteraction on parent/children — use robust lookup (components that implement IInteraction)
            IInteraction interactable = null;

            interactable = FindIInteractionOnGameObject(col.gameObject);

            if (interactable == null)
            {
                // parents
                Transform t = col.transform.parent;
                while (t != null && interactable == null)
                {
                    interactable = FindIInteractionOnGameObject(t.gameObject);
                    t = t.parent;
                }
            }

            if (interactable == null)
            {
                foreach (var mb in col.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb is IInteraction ii)
                    {
                        interactable = ii;
                        break;
                    }
                }
            }

            if (interactable != null)
            {
                Debug.Log($"[PlayerInteraction] (Proximity) Starting interaction with {interactable.GetType().Name} (robust lookup).");
                manager.inInteractionView = true;
                activeInteraction = interactable;
                activeInteraction.EnterInteraction(manager);
                isInteracting = true;
                manager.currentLayer = 0;
                return true;
            }
        }

        return false;
    }
}
