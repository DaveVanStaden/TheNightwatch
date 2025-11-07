using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BreakerBox : MonoBehaviour, IInteraction
{
    [Header("Camera / Zoom")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Transform zoomTarget;
    [SerializeField] private float zoomFOV = 40f;
    [SerializeField] private float transitionTime = 0.15f;

    [Header("Buttons")]
    [SerializeField] private BreakerButton[] buttons;

    [Header("Audio")]
    [SerializeField] private AudioSource swoosh;

    private PlayerManager currentPlayer;
    private Camera playerCamera;
    private bool isZoomed;
    private bool busy;

    public void EnterInteraction(PlayerManager playerManager)
    {
        if (interactionCamera == null || zoomTarget == null)
        {
            Debug.LogError("BreakerBox: interactionCamera or zoomTarget not assigned", this);
            return;
        }

        currentPlayer = playerManager;
        playerCamera = playerManager.playerCamera;

        // Snap interaction camera to player's camera so transition looks smooth
        if (playerCamera != null)
        {
            interactionCamera.transform.position = playerCamera.transform.position;
            interactionCamera.transform.rotation = playerCamera.transform.rotation;
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

        // disable flashlight movement if present
        var flashlightRotator = FindObjectOfType<FlashlightRotator>();
        if (flashlightRotator != null)
            flashlightRotator.canMove = false;

        PlaySwoosh();
        if (currentPlayer != null)
            currentPlayer.StartCoroutine(MoveToZoom());
    }

    public void UpdateInteraction(PlayerManager playerManager)
    {
        if (!isZoomed || busy) return;

        // Left click -> raycast from interaction camera to find BreakerButton
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var btn = hit.collider.GetComponent<BreakerButton>();
                if (btn != null)
                {
                    btn.Toggle();
                }
            }
        }

        // Allow Escape key to leave early (PlayerInteraction handles controller movement leave)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LeaveInteraction(playerManager);
        }
    }

    public void LeaveInteraction(PlayerManager playerManager)
    {
        if (currentPlayer != null)
            currentPlayer.StartCoroutine(LeaveRoutine());
        else
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
    }

    private IEnumerator LeaveRoutine()
    {
        busy = true;

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
                interactionCamera.transform.position = Vector3.Lerp(startPos, endPos, curve);
                interactionCamera.transform.rotation = Quaternion.Lerp(startRot, endRot, curve);
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

        var flashlightRotator = FindObjectOfType<FlashlightRotator>();
        if (flashlightRotator != null)
            flashlightRotator.canMove = true;

        PlaySwoosh();

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
}
