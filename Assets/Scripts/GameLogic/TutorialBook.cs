using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tutorial book interaction similar to BreakerBox:
/// - Player presses Interact while looking at the book -> PlayerInteraction will call EnterInteraction
/// - Camera moves smoothly to `zoomTarget` (uses the assigned `interactionCamera`)
/// - While active: A/D flip pages, S leaves interaction
/// - Page index is persisted per `bookId` via PlayerPrefs so the book "remembers" your last page
/// - Page flip animation rotates pages around an edge-hinge so pages visually flip left/right
/// </summary>
public class TutorialBook : MonoBehaviour, IInteraction
{
    [Header("Camera / Zoom")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Transform zoomTarget;
    [Tooltip("Transition time when moving camera into/out of the book view")]
    [SerializeField] private float transitionTime = 0.15f;

    [Header("Pages")]
    [Tooltip("Assign page GameObjects in order (0 = first). Each page must have a Renderer or Collider so the hinge can be computed.")]
    [SerializeField] private GameObject[] pages;
    [Tooltip("Time it takes to animate a single page flip")]
    [SerializeField] private float pageFlipTime = 0.25f;
    [Tooltip("Horizontal offset (in local page units) to nudge pages left when opened. Positive moves pages leftwards.")]
    [SerializeField] private float pageLeftOffset = 0.05f;

    [Header("Audio")]
    [SerializeField] private AudioSource swoosh;
    [SerializeField] private AudioClip flipClip;

    [Header("Persistence")]
    [Tooltip("Identifier used to persist last-read page (PlayerPrefs key = TutorialBook_{bookId}_page)")]
    [SerializeField] private string bookId = "DefaultTutorialBook";

    // internal state
    private PlayerManager currentPlayer;
    private Camera playerCamera;
    private bool isZoomed = false;
    private bool busy = false;
    private int currentPageIndex = 0;
    private List<GameObject> cachedFlashlightGOs = new();

    // runtime hinge parents for each page (created on EnterInteraction)
    private Transform[] pageHinges;

    private const string PrefKeyFormat = "TutorialBook_{0}_page";

    private void Awake()
    {
        if (pages == null) pages = new GameObject[0];
    }

    public void EnterInteraction(PlayerManager playerManager)
    {
        // Defensive camera resolution: if interactionCamera not assigned, try manager.interactionCamera or camera on this object
        if (interactionCamera == null)
        {
            interactionCamera = GetComponentInChildren<Camera>();
            if (interactionCamera == null && playerManager != null)
                interactionCamera = playerManager.interactionCamera;
        }

        if (interactionCamera == null || zoomTarget == null)
        {
            Debug.LogError("[TutorialBook] interactionCamera or zoomTarget not assigned", this);
            return;
        }

        currentPlayer = playerManager;
        playerCamera = playerManager.playerCamera;

        // Snap interaction camera to player's camera as starting point for the smooth move
        if (playerCamera != null)
        {
            interactionCamera.transform.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);
            interactionCamera.fieldOfView = playerCamera.fieldOfView;
        }

        // Ensure interaction camera GameObject is active and enable its Camera component.
        // Use GameObject.SetActive because the camera component can be present but the GameObject could be inactive.
        if (!interactionCamera.gameObject.activeSelf)
            interactionCamera.gameObject.SetActive(true);
        interactionCamera.enabled = true;

        var al = interactionCamera.GetComponent<AudioListener>();
        if (al != null) al.enabled = true;

        // Disable the player camera GameObject (safer than only toggling component).
        if (playerCamera != null)
        {
            if (playerCamera.gameObject.activeSelf)
                playerCamera.gameObject.SetActive(false);
            var pal = playerCamera.GetComponent<AudioListener>();
            if (pal != null) pal.enabled = false;
        }

        // disable flashlight GameObjects (same approach as BreakerBox)
        cachedFlashlightGOs.Clear();
        var flashlightRoot = FindObjectOfType<FlashlightRotator>()?.gameObject;
        if (flashlightRoot != null && flashlightRoot.activeSelf)
        {
            cachedFlashlightGOs.Add(flashlightRoot);
            flashlightRoot.SetActive(false);
        }
        else
        {
            var foundFlashlights = FindObjectsOfType<Flashlight>();
            foreach (var f in foundFlashlights)
            {
                if (f == null) continue;
                var go = f.gameObject;
                if (go.activeSelf)
                {
                    cachedFlashlightGOs.Add(go);
                    go.SetActive(false);
                }
            }
        }

        // load saved page index
        string key = string.Format(PrefKeyFormat, bookId);
        currentPageIndex = PlayerPrefs.GetInt(key, 0);
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, Mathf.Max(0, pages.Length - 1));

        // prepare hinges and initial page visuals
        EnsurePageHingesAndLayout();

        // Start move coroutine
        StartCoroutine(MoveToZoom());
        PlaySwoosh();
    }

    public void UpdateInteraction(PlayerManager playerManager)
    {
        if (interactionCamera == null) return;
        if (!isZoomed || busy) return;

        // Read inputs: A / D to change page, S to leave. Prefer InputSystem if available.
        bool pressedA = false, pressedD = false, pressedS = false;

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
            if (currentPlayer != null)
                currentPlayer.externalLeaveRequested = true;
            return;
        }

        if (pressedA)
        {
            // flip backwards (previous page becomes visible)
            int newIndex = Mathf.Clamp(currentPageIndex - 1, 0, pages.Length > 0 ? pages.Length - 1 : 0);
            if (newIndex != currentPageIndex)
            {
                // the page that visually flips back is the one before currentPageIndex
                int pageToFlipBack = currentPageIndex - 1;
                StartCoroutine(FlipPageBackward(pageToFlipBack));
                currentPageIndex = newIndex;
                PlayFlip();
            }
            return;
        }

        if (pressedD)
        {
            // flip forwards (current page flips away to reveal next)
            int newIndex = Mathf.Clamp(currentPageIndex + 1, 0, pages.Length > 0 ? pages.Length - 1 : 0);
            if (newIndex != currentPageIndex)
            {
                StartCoroutine(FlipPageForward(currentPageIndex));
                currentPageIndex = newIndex;
                PlayFlip();
            }
            return;
        }
    }

    public void LeaveInteraction(PlayerManager playerManager)
    {
        StartCoroutine(LeaveRoutine());
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
        float endFOV = interactionCamera.fieldOfView; // keep current FOV or change if desired

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

    private IEnumerator FlipPageForward(int pageIndex)
    {
        // Flip the page at pageIndex from 0 -> 180 around its left edge hinge so it visually moves left.
        if (pageIndex < 0 || pageIndex >= pages.Length) yield break;
        if (pageHinges == null) yield break;
        Transform hinge = pageHinges[pageIndex];
        if (hinge == null) yield break;

        busy = true;

        Quaternion start = hinge.localRotation;
        Quaternion end = start * Quaternion.Euler(0f, -180f, 0f); // rotate leftwards
        float elapsed = 0f;
        while (elapsed < pageFlipTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pageFlipTime);
            float s = Mathf.SmoothStep(0f, 1f, t);
            hinge.localRotation = Quaternion.Slerp(start, end, s);
            yield return null;
        }

        hinge.localRotation = end;
        busy = false;

        SaveCurrentPage();
    }

    private IEnumerator FlipPageBackward(int pageIndex)
    {
        // Flip the page at pageIndex from 180 -> 0 (unflip). pageIndex is the page to unflip.
        if (pageIndex < 0 || pageIndex >= pages.Length) yield break;
        if (pageHinges == null) yield break;
        Transform hinge = pageHinges[pageIndex];
        if (hinge == null) yield break;

        busy = true;

        Quaternion start = hinge.localRotation;
        Quaternion end = start * Quaternion.Euler(0f, 180f, 0f); // rotate rightwards to return
        float elapsed = 0f;
        while (elapsed < pageFlipTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pageFlipTime);
            float s = Mathf.SmoothStep(0f, 1f, t);
            hinge.localRotation = Quaternion.Slerp(start, end, s);
            yield return null;
        }

        hinge.localRotation = end;
        busy = false;

        SaveCurrentPage();
    }

    private IEnumerator LeaveRoutine()
    {
        busy = true;

        // Re-enable flashlight GameObjects we disabled
        if (cachedFlashlightGOs != null && cachedFlashlightGOs.Count > 0)
        {
            foreach (var go in cachedFlashlightGOs)
            {
                if (go != null) go.SetActive(true);
            }
            cachedFlashlightGOs.Clear();
        }

        // Move camera back to player smoothly (if player camera exists)
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

        // Re-enable the player camera GameObject and disable the interaction camera GameObject.
        if (currentPlayer != null && currentPlayer.playerCamera != null)
        {
            currentPlayer.playerCamera.gameObject.SetActive(true);
            var pal = currentPlayer.playerCamera.GetComponent<AudioListener>();
            if (pal != null) pal.enabled = true;
        }

        if (interactionCamera != null)
        {
            // disable the interaction camera GameObject to ensure no other scripts override component state
            interactionCamera.gameObject.SetActive(false);
            var al = interactionCamera.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }

        PlaySwoosh();

        // persist final page
        SaveCurrentPage();

        // Reset state
        isZoomed = false;
        busy = false;
        currentPlayer = null;
        yield return null;
    }

    private void SaveCurrentPage()
    {
        string key = string.Format(PrefKeyFormat, bookId);
        PlayerPrefs.SetInt(key, currentPageIndex);
        PlayerPrefs.Save();
    }

    private void PlaySwoosh()
    {
        if (swoosh == null) return;
        swoosh.pitch = Random.Range(.9f, 1.1f);
        swoosh.PlayOneShot(swoosh.clip);
    }

    private void PlayFlip()
    {
        if (swoosh == null || flipClip == null) return;
        swoosh.pitch = Random.Range(.9f, 1.1f);
        swoosh.PlayOneShot(flipClip);
    }

    // Create hinge parents for pages (only once) and position pages slightly left for visual book layout.
    private void EnsurePageHingesAndLayout()
    {
        if (pages == null) return;
        if (pageHinges == null || pageHinges.Length != pages.Length)
            pageHinges = new Transform[pages.Length];

        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            if (page == null) continue;

            // create hinge if missing
            if (pageHinges[i] == null)
            {
                GameObject hingeGO = new GameObject(page.name + "_Hinge");
                hingeGO.transform.SetParent(page.transform.parent, true);

                // compute approximate left-edge hinge position using Renderer or Collider bounds
                Renderer r = page.GetComponentInChildren<Renderer>();
                Collider c = page.GetComponentInChildren<Collider>();
                Vector3 hingeWorldPos = page.transform.position;
                if (r != null)
                {
                    // world-space left edge along page's local right vector
                    Vector3 right = page.transform.right;
                    float halfWidth = r.bounds.extents.x;
                    hingeWorldPos = r.bounds.center - right.normalized * halfWidth;
                }
                else if (c != null)
                {
                    Vector3 right = page.transform.right;
                    float halfWidth = c.bounds.extents.x;
                    hingeWorldPos = c.bounds.center - right.normalized * halfWidth;
                }
                else
                {
                    // fallback: offset from page center
                    hingeWorldPos = page.transform.position - page.transform.right * 0.5f;
                }

                hingeGO.transform.position = hingeWorldPos;
                hingeGO.transform.rotation = page.transform.rotation;

                // reparent page under hinge while preserving world transform
                page.transform.SetParent(hingeGO.transform, true);

                // small left offset so multiple pages don't overlap in the center
                page.transform.localPosition += Vector3.right * pageLeftOffset * -1f; // move left in hinge-local space
                pageHinges[i] = hingeGO.transform;

                // If page should start "turned", set hinge accordingly
                if (i < currentPageIndex)
                {
                    pageHinges[i].localRotation = Quaternion.Euler(0f, -180f, 0f);
                }
                else
                {
                    pageHinges[i].localRotation = Quaternion.identity;
                }
            }
        }
    }
}