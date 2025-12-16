using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Door that can be opened/closed via the Interact action.
/// Interaction is handled locally by listening to the PlayerManager's InputAction 'Interact'.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Door : MonoBehaviour
{
    public enum DoorState { Closed, Opening, Open, Closing, Locked }

    [Header("Door Rotation")]
    [Tooltip("Local Y angle to rotate to when open (degrees). Pivot should be at hinge.")]
    public float openAngle = 90f;
    [Tooltip("Speed in degrees per second when opening/closing.")]
    public float rotateSpeed = 180f;

    [Header("Auto close")]
    [Tooltip("Seconds the door remains open before auto-closing.")]
    public float autoCloseDelay = 10f;

    [Header("Locking")]
    [Tooltip("Whether the door starts locked.")]
    public bool locked = false;
    [Tooltip("Identifier of the key required to unlock this door. Leave empty for no key requirement.")]
    public string requiredKey = "";

    [Header("Audio / Animator (optional)")]
    public AudioSource audioSource;
    public AudioClip openClip;
    public AudioClip closeClip;
    public AudioClip lockedClip;
    public AudioClip unlockClip;
    public Animator animator;

    [Header("Debug")]
    [Tooltip("Enable debug logs for door interaction / rotation state.")]
    public bool debugLogs = false;

    [Header("Interaction options")]
    [Tooltip("If true, pressing Interact while the door is open (or opening) will manually close it and cancel the auto-close timer.")]
    public bool allowManualClose = true;

    // internal
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Coroutine rotateCoroutine;
    private Coroutine autoCloseCoroutine;
    private DoorState state = DoorState.Closed;

    // cached player manager for input + raycast
    private PlayerManager playerManager;

    private void Awake()
    {
        // ensure audio source
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // auto-find Animator if not assigned in inspector
        if (animator == null)
            animator = GetComponent<Animator>();

        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        // initial state
        state = locked ? DoorState.Locked : DoorState.Closed;

        // try to find player manager now
        playerManager = Object.FindFirstObjectByType<PlayerManager>();

        if (debugLogs) Debug.Log($"[Door:{name}] Awake. locked={locked} requiredKey='{requiredKey}' initialState={state}");
    }

    private void Update()
    {
        // listen for the Interact input via PlayerManager's inputActions
        if (playerManager != null && playerManager.inputActions != null)
        {
            var interact = playerManager.inputActions.Player.Interact;
            if (interact != null && interact.triggered)
            {
                TryInteract();
            }
        }
        else
        {
            // fallback: check new Input System keyboard first, otherwise fallback to legacy Input.GetKeyDown
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    TryInteract();
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryInteract();
                }
            }
        }
    }

    // Perform a center-screen raycast to determine if the player is looking at this door.
    private void TryInteract()
    {
        // Choose a camera for raycasting: prefer PlayerManager.rayCam then playerCamera then Camera.main
        Camera castCam = null;
        if (playerManager != null)
        {
            castCam = playerManager.rayCam != null ? playerManager.rayCam :
                      playerManager.playerCamera != null ? playerManager.playerCamera : null;
        }
        if (castCam == null) castCam = Camera.main;
        if (castCam == null)
        {
            if (debugLogs) Debug.LogWarning("[Door] No camera available for interaction raycast.");
            return;
        }

        // Ray from screen center
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = castCam.ScreenPointToRay(screenCenter);

        float maxRange = (playerManager != null) ? playerManager.interactionRange : 3f;
        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Collide))
        {
            // check if the hit is this door (or a child of this door)
            var hitDoor = hit.collider.GetComponentInParent<Door>();
            if (hitDoor == this)
            {
                if (debugLogs) Debug.Log($"[Door:{name}] Interact ray hit this door. Proceeding to handle interaction.");
                HandleInteraction();
            }
            else
            {
                if (debugLogs) Debug.Log($"[Door:{name}] Interact ray hit '{hit.collider.name}', not this door.");
            }
        }
        else
        {
            if (debugLogs) Debug.Log($"[Door:{name}] Interact raycast missed (range {maxRange}).");
        }
    }

    // Core door logic (previous EnterInteraction behavior moved here)
    private void HandleInteraction()
    {
        if (debugLogs) Debug.Log($"[Door:{name}] HandleInteraction called. state={state}");

        if (state == DoorState.Locked)
        {
            bool canUnlock = false;
            // If no requiredKey specified, allow unlocking if player has any key
            if (string.IsNullOrEmpty(requiredKey))
            {
                canUnlock = PlayerStats.Instance != null && PlayerStats.Instance.HasKey;
            }
            else
            {
                // use new method name to check specific key id
                canUnlock = PlayerStats.Instance != null && PlayerStats.Instance.HasKeyId(requiredKey);
            }

            if (canUnlock)
            {
                if (debugLogs) Debug.Log($"[Door:{name}] Player has required key ('{requiredKey}') - unlocking.");
                Unlock();
                PlayUnlockFeedback();
                StartOpening();
                // Note: key is not consumed by default. Remove it here if you want consumption:
                // PlayerStats.Instance.RemoveKey(requiredKey);
            }
            else
            {
                if (debugLogs) Debug.Log($"[Door:{name}] Locked and player lacks required key ('{requiredKey}') - playing locked feedback.");
                PlayLockedFeedback();
            }
        }
        else if (state == DoorState.Closed || state == DoorState.Closing)
        {
            if (debugLogs) Debug.Log($"[Door:{name}] Starting open sequence.");
            StartOpening();
        }
        else if (state == DoorState.Open || state == DoorState.Opening)
        {
            if (allowManualClose)
            {
                if (debugLogs) Debug.Log($"[Door:{name}] Manual close requested while open/opening. Cancelling auto-close and closing now.");
                // stop any auto-close countdown
                if (autoCloseCoroutine != null)
                {
                    StopCoroutine(autoCloseCoroutine);
                    autoCloseCoroutine = null;
                }
                // stop any current rotation coroutine to start closing cleanly
                if (rotateCoroutine != null)
                {
                    StopCoroutine(rotateCoroutine);
                    rotateCoroutine = null;
                }
                StartClosing();
            }
            else
            {
                // reset auto-close timer by re-starting it
                if (rotateCoroutine == null)
                {
                    if (debugLogs) Debug.Log($"[Door:{name}] Door already open - re-arming auto-close.");
                    if (autoCloseCoroutine != null)
                    {
                        StopCoroutine(autoCloseCoroutine);
                        autoCloseCoroutine = null;
                    }
                    autoCloseCoroutine = StartCoroutine(AutoCloseCountdown());
                }
            }
        }

        // If PlayerManager exists, signal it to leave any interaction view (mirrors previous behaviour)
        if (playerManager != null)
        {
            playerManager.externalLeaveRequested = true;
            if (debugLogs) Debug.Log($"[Door:{name}] Requested external leave on PlayerInteraction.");
        }
    }

    private void Unlock()
    {
        locked = false;
        state = DoorState.Closed;
        if (animator != null)
        {
            if (AnimatorHasTrigger("Unlock")) animator.SetTrigger("Unlock");
            else if (AnimatorHasBool("Unlock")) animator.SetBool("Unlock", true);
        }
    }

    private void PlayLockedFeedback()
    {
        if (animator != null)
        {
            if (AnimatorHasTrigger("LockedHit")) animator.SetTrigger("LockedHit");
            else if (AnimatorHasBool("LockedHit")) animator.SetBool("LockedHit", true);
        }

        if (audioSource != null && lockedClip != null)
            audioSource.PlayOneShot(lockedClip);
    }

    private void PlayUnlockFeedback()
    {
        if (animator != null)
        {
            if (AnimatorHasTrigger("Unlock")) animator.SetTrigger("Unlock");
            else if (AnimatorHasBool("Unlock")) animator.SetBool("Unlock", true);
        }

        if (audioSource != null && unlockClip != null)
            audioSource.PlayOneShot(unlockClip);
    }

    private void StartOpening()
    {
        // cancel existing rotate coroutine
        if (rotateCoroutine != null)
            StopCoroutine(rotateCoroutine);

        if (animator != null)
        {
            // prefer triggers (your Animator uses Trigger parameters for Open/Close)
            if (AnimatorHasTrigger("Open"))
            {
                animator.SetTrigger("Open");
            }
            else if (AnimatorHasBool("Open"))
            {
                // fallback if Animator was configured with bools
                animator.SetBool("Open", true);
                if (AnimatorHasBool("Close")) animator.SetBool("Close", false);
            }
        }

        if (audioSource != null && openClip != null)
            audioSource.PlayOneShot(openClip);

        rotateCoroutine = StartCoroutine(RotateTo(openRotation, () =>
        {
            state = DoorState.Open;

            // start auto close countdown (replace any existing countdown)
            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }
            autoCloseCoroutine = StartCoroutine(AutoCloseCountdown());
            rotateCoroutine = null;

            if (debugLogs) Debug.Log($"[Door:{name}] Finished opening. state={state}");
        }));
        state = DoorState.Opening;
    }

    private void StartClosing()
    {
        if (rotateCoroutine != null)
            StopCoroutine(rotateCoroutine);

        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        if (animator != null)
        {
            if (AnimatorHasTrigger("Close"))
            {
                animator.SetTrigger("Close");
            }
            else if (AnimatorHasBool("Close"))
            {
                animator.SetBool("Close", true);
                if (AnimatorHasBool("Open")) animator.SetBool("Open", false);
            }
        }

        if (audioSource != null && closeClip != null)
            audioSource.PlayOneShot(closeClip);

        rotateCoroutine = StartCoroutine(RotateTo(closedRotation, () =>
        {
            state = locked ? DoorState.Locked : DoorState.Closed;

            rotateCoroutine = null;

            if (debugLogs) Debug.Log($"[Door:{name}] Finished closing. state={state}");
        }));
        state = DoorState.Closing;
    }

    private IEnumerator RotateTo(Quaternion target, System.Action onComplete)
    {
        if (debugLogs) Debug.Log($"[Door:{name}] RotateTo() starting. target angle={Quaternion.Angle(transform.localRotation, target):F1}");

        // rotate smoothly from current local rotation to target
        while (Quaternion.Angle(transform.localRotation, target) > 0.5f)
        {
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, rotateSpeed * Time.deltaTime);
            yield return null;
        }
        transform.localRotation = target;
        onComplete?.Invoke();
    }

    private IEnumerator AutoCloseCountdown()
    {
        float elapsed = 0f;
        while (elapsed < autoCloseDelay)
        {
            // if door got closed or locked via other logic, bail out
            if (state != DoorState.Open)
            {
                autoCloseCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // begin closing if still open
        if (state == DoorState.Open)
        {
            autoCloseCoroutine = null;
            StartClosing();
        }
        else
        {
            autoCloseCoroutine = null;
        }
    }

    // Helper: check for trigger parameter presence to avoid animator errors when parameter missing
    private bool AnimatorHasTrigger(string paramName)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == paramName) return true;
        }
        return false;
    }

    // Helper: check for bool parameter presence (fallback)
    private bool AnimatorHasBool(string paramName)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName) return true;
        }
        return false;
    }

#if UNITY_EDITOR
    // Editor helper to quickly toggle lock state (optional)
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // update saved rotations when inspector values change
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }
    }
#endif
}