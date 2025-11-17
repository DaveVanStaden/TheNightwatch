using System.Collections;
using UnityEngine;

public class Interactable : MonoBehaviour, IInteraction
{
    [SerializeField] GameObject[] setAngles;
    FlashlightRotator flashlightRotator;
    GameObject flashlight;
    public bool busy;

    MonitorCursor lastCursor;
    CamData previousAngle;

    AudioSource swoosh;

    public Camera interactionCamera; // Assign in Inspector or dynamically

    private bool isFollowingPlayer = false;
    private bool isInInteractionView = false;
    private PlayerManager currentPlayerManager;

    private void Start()
    {
        flashlightRotator = FindAnyObjectByType<FlashlightRotator>();
        flashlight = flashlightRotator != null ? flashlightRotator.gameObject : null;
        swoosh = GetComponent<AudioSource>();
    }

    private void LateUpdate()
    {
        // Intentionally empty � Interactable is driven by PlayerInteraction / coroutines.
    }

    // IInteraction implementation

    public void EnterInteraction(PlayerManager playerManager)
    {
        if (interactionCamera == null)
        {
            Debug.LogError("InteractionCamera is not assigned!", this);
            return;
        }

        // Snap to player camera immediately
        if (playerManager.playerCamera != null)
        {
            interactionCamera.transform.SetPositionAndRotation(playerManager.playerCamera.transform.position, playerManager.playerCamera.transform.rotation);
            interactionCamera.fieldOfView = playerManager.playerCamera.fieldOfView;
        }

        // Enable interaction camera, disable player camera
        interactionCamera.enabled = true;
        if (playerManager.playerCamera != null)
        {
            playerManager.playerCamera.enabled = false;
            var al = playerManager.playerCamera.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }

        if (flashlightRotator != null)
            flashlightRotator.canMove = false;

        PlaySound();

        // Start following the player (used while moving into the view)
        currentPlayerManager = playerManager;
        isFollowingPlayer = true;
        isInInteractionView = false;

        if (flashlight != null)
            flashlight.SetActive(false);

        // Start the transition to interaction view
        StartCoroutine(MoveToInteractionView());
    }

    public void UpdateInteraction(PlayerManager playerManager)
    {
        // Default: while following the player prior to settling into interaction view, copy camera
        if (isFollowingPlayer && !isInInteractionView && currentPlayerManager != null && currentPlayerManager.playerCamera != null && interactionCamera != null)
        {
            interactionCamera.transform.SetPositionAndRotation(currentPlayerManager.playerCamera.transform.position, currentPlayerManager.playerCamera.transform.rotation);
            interactionCamera.fieldOfView = currentPlayerManager.playerCamera.fieldOfView;
        }
    }

    public void LeaveInteraction(PlayerManager playerManager)
    {
        // Leave is handled asynchronously by the manager via LeaveTheThing coroutine.
        // Keep this method empty so PlayerInteraction/manager controls the exit flow.
    }

    private IEnumerator MoveToInteractionView()
    {
        // Stop following and lerp to angle(0)
        isInInteractionView = true;
        isFollowingPlayer = false;

        var audioListener = interactionCamera.GetComponent<AudioListener>();
        if (audioListener != null)
            audioListener.enabled = true;

        int angle = 0;
        interactionCamera.transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        float startFOV = interactionCamera.fieldOfView;

        Vector3 endPos = setAngles[angle].transform.position;
        Quaternion endRot = setAngles[angle].transform.rotation;
        float endFOV = setAngles[angle].GetComponent<CamData>().newFOV;

        float lerpTime = 0.15f;
        float elapsed = 0f;

        PlaySound();
        CheckCursor(setAngles[angle].GetComponent<CamData>());

        while (elapsed < lerpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lerpTime);
            float curveT = 1f - Mathf.Pow(1f - t, 3f); // Ease-out

            interactionCamera.transform.position = Vector3.Lerp(startPos, endPos, curveT);
            interactionCamera.transform.rotation = Quaternion.Lerp(startRot, endRot, curveT);
            interactionCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curveT);
            yield return null;
        }

        // Snap to final position
        interactionCamera.transform.SetPositionAndRotation(endPos, endRot);
        interactionCamera.fieldOfView = endFOV;

        yield break;
    }

    public IEnumerator LeaveTheThing(PlayerManager playerManager)
    {
        if (lastCursor != null)
            DisableLastCursor();

        if (flashlight != null)
            flashlight.SetActive(true);

        busy = true;

        // Lerp from interaction view back to player camera
        if (interactionCamera != null && playerManager.playerCamera != null)
        {
            interactionCamera.transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
            float startFOV = interactionCamera.fieldOfView;

            playerManager.playerCamera.transform.GetPositionAndRotation(out Vector3 endPos, out Quaternion endRot);
            float endFOV = playerManager.playerCamera.fieldOfView;

            float lerpTime = 0.15f;
            float elapsed = 0f;

            PlaySound();

            while (elapsed < lerpTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lerpTime);
                float curveT = 1f - Mathf.Pow(1f - t, 3f); // Ease-out

                interactionCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, curveT), Quaternion.Lerp(startRot, endRot, curveT));
                interactionCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curveT);
                yield return null;
            }
        }

        // Enable player camera, disable interaction camera
        if (playerManager.playerCamera != null)
        {
            playerManager.playerCamera.enabled = true;
            var al = playerManager.playerCamera.GetComponent<AudioListener>();
            if (al != null) al.enabled = true;
        }
        if (interactionCamera != null)
        {
            interactionCamera.enabled = false;
            var ia = interactionCamera.GetComponent<AudioListener>();
            if (ia != null) ia.enabled = false;
        }

        float maxTime = .2f;
        yield return new WaitForSeconds(maxTime);

        busy = false;
        if (flashlightRotator != null) flashlightRotator.canMove = true;
        currentPlayerManager = null;
        isFollowingPlayer = false;
        isInInteractionView = false;
        yield return null;
    }

    public IEnumerator SetAngle(int angle)
    {
        //Disable rendering on previous angle if applicable
        if (previousAngle != null)
        {
            previousAngle.DisableRendering();
            print(previousAngle.name);
        }
        if (interactionCamera == null) yield break;

        CamData camera = setAngles[angle].GetComponent<CamData>();
        PlaySound();
        float timePassed = 0f;
        float pos;
        float maxTime = .15f;

        CheckCursor(camera);
        previousAngle = camera;
        previousAngle.EnableRendering();
        while (timePassed < maxTime)
        {
            timePassed += Time.deltaTime;
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);
            interactionCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(interactionCamera.transform.position, setAngles[angle].transform.position, pos),
                Quaternion.Lerp(interactionCamera.transform.rotation, setAngles[angle].transform.rotation, pos)
            );
            if (camera != null)
            {
                interactionCamera.fieldOfView = Mathf.Lerp(interactionCamera.fieldOfView, camera.newFOV, pos);
            }
            yield return null;
        }
        yield return new WaitForSeconds(maxTime);
    }

    private void CheckCursor(CamData angle)
    {
        DisableLastCursor();
        if (angle != null && angle.hasCursor)
        {
            if (angle.cursor != null && angle.cursor.GetComponent<MonitorCursor>() != null)
            {
                lastCursor = angle.cursor.GetComponent<MonitorCursor>();
                lastCursor.EnableCursorControl();
            }
        }
    }

    private void DisableLastCursor()
    {
        if (lastCursor != null)
            lastCursor.DisableCursorControl();
    }

    public void PlaySound()
    {
        if (swoosh == null) swoosh = GetComponent<AudioSource>();
        if (swoosh == null) return;
        swoosh.pitch = Random.Range(.20f, .30f);
        swoosh.PlayOneShot(swoosh.clip);
    }
}
