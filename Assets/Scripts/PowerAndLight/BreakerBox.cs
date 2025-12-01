using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BreakerBox : MonoBehaviour, IInteraction
{
    [Header("Camera / Zoom")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Transform zoomTarget;
    [SerializeField] private Transform leftZoomTarget;
    [SerializeField] private Transform rightZoomTarget;
    [SerializeField] private float zoomFOV = 40f;
    [SerializeField] private float transitionTime = 0.15f;

    [Header("Buttons")]
    [SerializeField] private BreakerButton[] buttons;
    [Header("Power Groups")]
    [SerializeField] private PowerGroups[] powerGroups;

    [Header("Debug")]
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private Color debugRayColor = Color.cyan;
    [SerializeField] private float debugRayWidth = 0.02f;
    [SerializeField] private float debugRayDuration = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource swoosh;
    [SerializeField] private AudioSource squeak;
    [SerializeField] private AudioClip open;
    [SerializeField] private AudioClip close;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    private bool doorOpen;

    // Replace single cachedFlashlightGO with a list to track all flashlight GameObjects we disable
    [Header("Flashlight")]
    [SerializeField] private GameObject flashlightRoot; // assign an empty GameObject that parents the flashlight; optional
    private List<GameObject> cachedFlashlightGOs = new List<GameObject>();

    private PlayerManager currentPlayer;
    private Camera playerCamera;
    private bool isZoomed;
    private bool busy;

    // camera angle state: 0 = left, 1 = center, 2 = right
    private int currentAngleIndex = 1;

    public void EnterInteraction(PlayerManager playerManager)
    {
        //Debug.Log("[BreakerBox] EnterInteraction called on " + name + " by " + (playerManager != null ? playerManager.name : "null"));

        if (interactionCamera == null || zoomTarget == null)
        {
            Debug.LogError("BreakerBox: interactionCamera or zoomTarget not assigned", this);
            return;
        }

        currentPlayer = playerManager;
        playerCamera = playerManager.playerCamera;

        if (playerCamera != null)
        {
            interactionCamera.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);
            interactionCamera.fieldOfView = playerCamera.fieldOfView;
        }

        interactionCamera.enabled = true;
        if (interactionCamera.GetComponent<AudioListener>() != null)
            interactionCamera.GetComponent<AudioListener>().enabled = true;

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
            if (playerCamera.GetComponent<AudioListener>() != null)
                playerCamera.GetComponent<AudioListener>().enabled = false;
        }

        var flashlightRotator = FindAnyObjectByType<FlashlightRotator>();
        if (flashlightRotator != null)
            flashlightRotator.canMove = false;

        PlaySwoosh();
        PlaySqueak();
        if (animator != null)
        {
            doorOpen = true;
            ToggleOpen();
        }
        else Debug.LogError("[BreakerBox] No animator assigned in inspector");

        // Cache & disable flashlight GameObjects:
        // - if flashlightRoot assigned: disable that root (and remember it) so it can be re-enabled later.
        // - otherwise find all Flashlight components in scene and disable their GameObjects (only if currently active).
        cachedFlashlightGOs.Clear();

        if (flashlightRoot != null)
        {
            if (flashlightRoot.activeSelf)
            {
                cachedFlashlightGOs.Add(flashlightRoot);
                flashlightRoot.SetActive(false);
                Debug.Log("[BreakerBox] Disabled assigned flashlightRoot GameObject for interaction.");
            }
            else
            {
                Debug.Log("[BreakerBox] Assigned flashlightRoot was already inactive.");
            }
        }
        else
        {
            var foundFlashlights = FindObjectsByType<Flashlight>(FindObjectsSortMode.None);
            int disabledCount = 0;
            foreach (var f in foundFlashlights)
            {
                if (f == null) continue;
                var go = f.gameObject;
                if (go.activeSelf)
                {
                    cachedFlashlightGOs.Add(go);
                    go.SetActive(false);
                    disabledCount++;
                }
            }
            Debug.Log($"[BreakerBox] Disabled {disabledCount} Flashlight GameObject(s) for interaction (fallback).");
        }

        // ensure we start centered
        currentAngleIndex = 1;

        // Start camera move coroutine on this BreakerBox (moves to center/zoomTarget)
        StartCoroutine(MoveToZoom());
    }

    public void UpdateInteraction(PlayerManager playerManager)
    {
        // quick guard
        if (interactionCamera == null) return;

        if (!isZoomed || busy) return;

        // --- WASD navigation handling (S to leave, A/D to switch camera angles) ---
        bool pressedA = false;
        bool pressedD = false;
        bool pressedS = false;

        if (Keyboard.current != null)
        {
            pressedA = Keyboard.current.aKey.wasPressedThisFrame;
            pressedD = Keyboard.current.dKey.wasPressedThisFrame;
            pressedS = Keyboard.current.sKey.wasPressedThisFrame;
        }
        else
        {
            pressedA = Input.GetKeyDown(KeyCode.A);
            pressedD = Input.GetKeyDown(KeyCode.D);
            pressedS = Input.GetKeyDown(KeyCode.S);
        }

        if (pressedS)
        {
            // Request the player to leave interaction via PlayerInteraction so it handles cleanup
            // (prevents the player from remaining "stuck" in interaction state).
            if (currentPlayer != null)
                currentPlayer.externalLeaveRequested = true;
            return;
        }

        if (pressedA || pressedD)
        {
            // rotate between three camera angles: left(0), center(1), right(2)
            int delta = pressedA ? -1 : +1;
            int newIndex = (currentAngleIndex + delta + 3) % 3;

            // determine target transform for the index
            Transform target = GetTransformForAngleIndex(newIndex);
            if (target != null)
            {
                // Use same movement curve as entering (fast start, slower near end)
                StartCoroutine(TransitionToAngle(target, zoomFOV));
                currentAngleIndex = newIndex;
                PlaySwoosh();
            }
            else
            {
                Debug.LogWarning("[BreakerBox] Requested camera angle target is not assigned.");
            }

            // input consumed - don't process clicks this frame
            return;
        }

        // read input (Input System preferred) for clicking
        bool click;
        Vector2 mousePos;
        if (Mouse.current != null)
        {
            click = Mouse.current.leftButton.wasPressedThisFrame;
            mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            click = Input.GetMouseButtonDown(0);
            mousePos = Input.mousePosition;
        }

        // also accept Fire action on PlayerInput if available
        bool fire = false;
        if (playerManager != null && playerManager.inputActions != null)
        {
            try { fire = playerManager.inputActions.Player.Fire.triggered; } catch { fire = false; }
        }

        if (!click && !fire) return;

        if (fire && mousePos == Vector2.zero)
            mousePos = new Vector2(interactionCamera.pixelWidth * 0.5f, interactionCamera.pixelHeight * 0.5f);

        // Build ray from interaction camera using the mouse position (supports RenderTexture and non-fullscreen cameras)
        Rect camRect = interactionCamera.pixelRect;
        Vector2 viewportPoint;

        // If using a RenderTexture, mouse is in screen space � map via Screen size.
        // Otherwise map mouse into the camera's pixelRect.
        if (interactionCamera.targetTexture != null)
        {
            viewportPoint = new Vector2(mousePos.x / (float)Screen.width, mousePos.y / (float)Screen.height);
        }
        else
        {
            viewportPoint = new Vector2((mousePos.x - camRect.x) / camRect.width, (mousePos.y - camRect.y) / camRect.height);
        }

        // clamp to valid viewport
        viewportPoint.x = Mathf.Clamp01(viewportPoint.x);
        viewportPoint.y = Mathf.Clamp01(viewportPoint.y);

        // Debug info to help diagnose coordinate mapping
        //Debug.Log($"[BreakerBox] Camera pixelRect={camRect}, Screen={Screen.width}x{Screen.height}, mouse={mousePos}, viewport={viewportPoint}");

        // Create ray from viewport point
        Ray ray = interactionCamera.ViewportPointToRay(viewportPoint);

        // Print ray info for debugging
        //Debug.Log($"[BreakerBox] Click ray (viewport): origin={ray.origin}, dir={ray.direction}, mouse={mousePos}, viewport={viewportPoint}");

        // Scene view debug (keeps simple editor debug line; renderer visual removed)
        Debug.DrawRay(ray.origin, ray.direction * 50f, debugRayColor, 2f);

        // Raycast handling (no runtime debug line renderer used)
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableMask, QueryTriggerInteraction.Collide))
        {
            //Debug.Log($"[BreakerBox] Click ray hit: {hit.collider.name}");

            // ONLY toggle if the exact collider hit has a BreakerButton component (no parent/child fallbacks)
            var hitColliderGO = hit.collider.gameObject;
            var btn = hitColliderGO.GetComponent<BreakerButton>();
            if (btn != null)
            {
                //Debug.Log($"[BreakerBox] Hit exact BreakerButton on '{btn.gameObject.name}' � calling Toggle()");
                btn.Toggle();
                return;
            }

            // Do not toggle all groups for arbitrary hits anymore � ignore other hits
            Debug.Log("[BreakerBox] Click hit something else � no action taken.");
        }
        else
        {
            Debug.LogError($"[BreakerBox] Click raycast missed at screen {mousePos}");
        }
    }

    private Transform GetTransformForAngleIndex(int index)
    {
        // 0 = left, 1 = center, 2 = right
        switch (index)
        {
            case 0: return leftZoomTarget != null ? leftZoomTarget : zoomTarget;
            case 1: return zoomTarget;
            case 2: return rightZoomTarget != null ? rightZoomTarget : zoomTarget;
            default: return zoomTarget;
        }
    }

    private IEnumerator MoveToZoom()
    {
        busy = true;
        isZoomed = true;

        Vector3 startPos = interactionCamera.transform.position;
        Quaternion startRot = interactionCamera.transform.rotation;
        float startFOV = interactionCamera.fieldOfView;

        Vector3 endPos = zoomTarget.position;
        Quaternion endRot = zoomTarget.rotation;
        float endFOV = zoomFOV;

        float elapsed = 0f;
        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionTime);
            float curve = 1f - Mathf.Pow(1f - t, 3f);
            interactionCamera.transform.position = Vector3.Lerp(startPos, endPos, curve);
            interactionCamera.transform.rotation = Quaternion.Lerp(startRot, endRot, curve);
            interactionCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curve);
            yield return null;
        }

        interactionCamera.transform.position = endPos;
        interactionCamera.transform.rotation = endRot;
        interactionCamera.fieldOfView = endFOV;
        busy = false;

        //Debug.Log("[BreakerBox] MoveToZoom finished � interaction is now active and clickable.");
    }

    private IEnumerator TransitionToAngle(Transform target, float targetFOV)
    {
        if (target == null) yield break;

        busy = true;

        Vector3 startPos = interactionCamera.transform.position;
        Quaternion startRot = interactionCamera.transform.rotation;
        float startFOV = interactionCamera.fieldOfView;

        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;
        float endFOV = targetFOV;

        float elapsed = 0f;
        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionTime);
            float curve = 1f - Mathf.Pow(1f - t, 3f);
            interactionCamera.transform.position = Vector3.Lerp(startPos, endPos, curve);
            interactionCamera.transform.rotation = Quaternion.Lerp(startRot, endRot, curve);
            interactionCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curve);
            yield return null;
        }

        interactionCamera.transform.position = endPos;
        interactionCamera.transform.rotation = endRot;
        interactionCamera.fieldOfView = endFOV;

        busy = false;
    }

    private void ToggleAllPowerGroups()
    {
        if (powerGroups == null || powerGroups.Length == 0)
        {
            Debug.LogWarning("[BreakerBox] No PowerGroups assigned to this BreakerBox.");
            return;
        }

        bool anyOn = false;
        foreach (var pg in powerGroups)
        {
            if (pg != null && pg.AnyLightOn())
            {
                anyOn = true;
                break;
            }
        }

        if (anyOn)
        {
            foreach (var pg in powerGroups) if (pg != null) pg.TurnOffLights();
            Debug.Log("[BreakerBox] Turned OFF all assigned power groups.");
        }
        else
        {
            foreach (var pg in powerGroups) if (pg != null) pg.TurnOnLights();
            Debug.Log("[BreakerBox] Turned ON all assigned power groups.");
        }
    }

    public void LeaveInteraction(PlayerManager playerManager)
    {
        //Debug.Log("[BreakerBox] LeaveInteraction called on " + name);
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        busy = true;
        // Re-enable flashlight GameObject if we disabled it on EnterInteraction
        if (cachedFlashlightGOs != null && cachedFlashlightGOs.Count > 0)
        {
            foreach (var go in cachedFlashlightGOs)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            //Debug.Log($"[BreakerBox] Re-enabled {cachedFlashlightGOs.Count} Flashlight GameObject(s) after interaction.");
            cachedFlashlightGOs.Clear();
        }

        if (currentPlayer != null && currentPlayer.playerCamera != null)
        {
            Vector3 startPos = interactionCamera.transform.position;
            Quaternion startRot = interactionCamera.transform.rotation;
            float startFOV = interactionCamera.fieldOfView;

            Vector3 endPos = currentPlayer.playerCamera.transform.position;
            Quaternion endRot = currentPlayer.playerCamera.transform.rotation;
            float endFOV = currentPlayer.playerCamera.fieldOfView;

            float elapsed = 0f;
            while (elapsed < transitionTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionTime);
                float curve = 1f - Mathf.Pow(1f - t, 3f);
                interactionCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, curve), Quaternion.Lerp(startRot, endRot, curve));
                interactionCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curve);
                yield return null;
            }
        }

        if (currentPlayer != null && currentPlayer.playerCamera != null)
        {
            currentPlayer.playerCamera.enabled = true;
            if (currentPlayer.playerCamera.GetComponent<AudioListener>() != null)
                currentPlayer.playerCamera.GetComponent<AudioListener>().enabled = true;
        }

        if (interactionCamera != null)
        {
            interactionCamera.enabled = false;
            if (interactionCamera.GetComponent<AudioListener>() != null)
                interactionCamera.GetComponent<AudioListener>().enabled = false;
        }

        var flashlightRotator = FindAnyObjectByType<FlashlightRotator>();
        if (flashlightRotator != null)
            flashlightRotator.canMove = true;

        PlaySwoosh();
        PlaySqueak();
        if (animator != null)
        {
            doorOpen = false;
            ToggleOpen();
        }
        else Debug.LogError("[BreakerBox] No animator assigned in inspector");

        // Always reset angle index so next EnterInteraction starts centered
        currentAngleIndex = 1;

        busy = false;
        isZoomed = false;
        currentPlayer = null;
        yield return null;
    }

    private void PlaySwoosh()
    {
        if (swoosh == null) return;
        swoosh.pitch = Random.Range(.20f, .30f);
        swoosh.PlayOneShot(swoosh.clip);
    }

    private void ToggleOpen()
    {
        animator.SetBool("Door", doorOpen);
    }
    private void PlaySqueak()
    {
        if (squeak == null) return;
        if (!doorOpen)
        {
            squeak.PlayOneShot(open);
        }
        else squeak.PlayOneShot(close); 
    }
}