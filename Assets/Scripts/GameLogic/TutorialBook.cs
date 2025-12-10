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
    [Tooltip("Assign page GameObjects in order (0 = first). Each page must have a TutorialPage component or will get one added.")]
    [SerializeField] private GameObject[] pages;
    [Tooltip("Time it takes to animate a single page flip")]
    [SerializeField] private float pageFlipTime = 0.25f;

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

    // TutorialPage components (one per page GameObject)
    private TutorialPage[] pageComponents;

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

        // Prefer FindObjectsByType (non-deprecated) to locate Flashlight instances.
        // Use FindFirstObjectForType for the rotator root (also non-deprecated).
        var foundFlashlights = FindObjectsByType<Flashlight>(FindObjectsSortMode.None);
        var flashlightRotator = FindFirstObjectByType<FlashlightRotator>();
        var flashlightRoot = flashlightRotator != null ? flashlightRotator.gameObject : null;

        if (flashlightRoot != null && flashlightRoot.activeSelf)
        {
            cachedFlashlightGOs.Add(flashlightRoot);
            flashlightRoot.SetActive(false);
        }
        else
        {
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

        // prepare TutorialPage components and set initial states
        EnsurePageComponentsAndInitialState();

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
                int pageToFlipBack = currentPageIndex - 1;
                if (pageComponents != null && pageToFlipBack >= 0 && pageToFlipBack < pageComponents.Length && pageComponents[pageToFlipBack] != null)
                {
                    StartCoroutine(StartFlipBackward(pageComponents[pageToFlipBack]));
                }
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
                if (pageComponents != null && currentPageIndex >= 0 && currentPageIndex < pageComponents.Length && pageComponents[currentPageIndex] != null)
                {
                    StartCoroutine(StartFlipForward(pageComponents[currentPageIndex]));
                }
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

    private IEnumerator StartFlipForward(TutorialPage page)
    {
        if (page == null) yield break;
        busy = true;
        yield return page.FlipForward(pageFlipTime);
        busy = false;
        SaveCurrentPage();
    }

    private IEnumerator StartFlipBackward(TutorialPage page)
    {
        if (page == null) yield break;
        busy = true;
        yield return page.FlipBackward(pageFlipTime);
        busy = false;
        SaveCurrentPage();
    }

    private IEnumerator LeaveRoutine()
    {
        busy = true;
        PlaySwoosh();

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
        swoosh.pitch = Random.Range(.20f, .30f);
        swoosh.volume = .01f;
        swoosh.PlayOneShot(swoosh.clip);
    }

    private void PlayFlip()
    {
        if (swoosh == null || flipClip == null) return;
        swoosh.pitch = Random.Range(.9f, 1.1f);
        swoosh.volume = .1f;
        swoosh.PlayOneShot(flipClip);
    }

    // Ensure every page GameObject has a TutorialPage component and set initial flipped state
    private void EnsurePageComponentsAndInitialState()
    {
        if (pages == null) return;

        pageComponents = new TutorialPage[pages.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            var pg = pages[i];
            if (pg == null) continue;

            var comp = pg.GetComponent<TutorialPage>();
            if (comp == null)
            {
                // add the component and let it cache original transform on Awake
                comp = pg.AddComponent<TutorialPage>();
            }
            pageComponents[i] = comp;

            // initial state: pages with index < currentPageIndex are considered turned/flipped
            bool flipped = i < currentPageIndex;
            comp.SetImmediateFlipped(flipped);
        }
    }
}