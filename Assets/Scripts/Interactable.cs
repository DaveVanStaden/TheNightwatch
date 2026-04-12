using System.Collections;
using UnityEngine;

public class Interactable : MonoBehaviour, IInteraction
{
    [SerializeField] GameObject[] setAngles;
    FlashlightRotator flashlightRotator;
    GameObject flashlight;
    public bool busy;

    MonitorCursor lastCursor;

    AudioSource swoosh;

    public Camera interactionCamera; // Assign in Inspector or dynamically

    private bool isFollowingPlayer = false;
    private bool isInInteractionView = false;
    private PlayerManager currentPlayerManager;

    private void Start()
    {
        flashlightRotator = FindObjectOfType<FlashlightRotator>();
        flashlight = flashlightRotator.gameObject;
        swoosh = GetComponent<AudioSource>();
    }

    private void LateUpdate()
    {

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
           interactionCamera.transform.position = playerManager.playerCamera.transform.position;
            interactionCamera.transform.rotation = playerManager.playerCamera.transform.rotation;
            interactionCamera.fieldOfView = playerManager.playerCamera.fieldOfView; 
        }

        // Enable interaction camera, disable player camera
        interactionCamera.enabled = true;
        if (playerManager.playerCamera != null)
        {
            playerManager.playerCamera.enabled = false;
            playerManager.playerCamera.GetComponent<AudioListener>().enabled = false;
        }

        flashlightRotator.canMove = false;
        PlaySound();

        // Start following the player
        currentPlayerManager = playerManager;
        isFollowingPlayer = true;
        isInInteractionView = false;

        flashlight.SetActive(false);

        // Start the transition to interaction view
        StartCoroutine(MoveToInteractionView());
    }

    public void UpdateInteraction(PlayerManager playerManager)
    {
        if (isFollowingPlayer && !isInInteractionView && currentPlayerManager != null && currentPlayerManager.playerCamera != null && interactionCamera != null)
        {
            interactionCamera.transform.position = currentPlayerManager.playerCamera.transform.position;
            interactionCamera.transform.rotation = currentPlayerManager.playerCamera.transform.rotation;
            interactionCamera.fieldOfView = currentPlayerManager.playerCamera.fieldOfView;
        }
    }

    public void LeaveInteraction(PlayerManager playerManager)
    {
        StartCoroutine(LeaveTheThing(playerManager));
    }


    private IEnumerator MoveToInteractionView()
    {
        // Stop following and lerp to angle(0)
        isInInteractionView = true;
        isFollowingPlayer = false;
        interactionCamera.GetComponent<AudioListener>().enabled = true;


        int angle = 0;
        Vector3 startPos = interactionCamera.transform.position;
        Quaternion startRot = interactionCamera.transform.rotation;
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
        interactionCamera.transform.position = endPos;
        interactionCamera.transform.rotation = endRot;
        interactionCamera.fieldOfView = endFOV;
    }

    public IEnumerator LeaveTheThing(PlayerManager playerManager)
    {
        DisableLastCursor();
        flashlight.SetActive(true);
        busy = true;

        // Lerp from interaction view back to player camera
        if (interactionCamera != null && playerManager.playerCamera != null)
        {
            Vector3 startPos = interactionCamera.transform.position;
            Quaternion startRot = interactionCamera.transform.rotation;
            float startFOV = interactionCamera.fieldOfView;

            Vector3 endPos = playerManager.playerCamera.transform.position;
            Quaternion endRot = playerManager.playerCamera.transform.rotation;
            float endFOV = playerManager.playerCamera.fieldOfView;

            float lerpTime = 0.15f;
            float elapsed = 0f;

            PlaySound();

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
        }

        // Enable player camera, disable interaction camera
        if (playerManager.playerCamera != null)
        {
            playerManager.playerCamera.enabled = true;
            playerManager.playerCamera.GetComponent<AudioListener>().enabled = true;

        }
        if (interactionCamera != null)
        {
            interactionCamera.enabled = false;
            interactionCamera.GetComponent<AudioListener>().enabled = false;
        }

        float maxTime = .2f;
        yield return new WaitForSeconds(maxTime);
        busy = false;
        flashlightRotator.canMove = true;
        flashlight.SetActive(true);
        currentPlayerManager = null;
        isFollowingPlayer = false;
        isInInteractionView = false;
        yield return null;
    }

    public IEnumerator SetAngle(int angle)
    {
        if (interactionCamera == null) yield break;

        CamData camera = setAngles[angle].GetComponent<CamData>();
        PlaySound();
        float timePassed = 0f;
        float pos;
        float maxTime = .15f;

        CheckCursor(camera);

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

    private void CheckCursor(CamData camera)
    {
        DisableLastCursor();
        if (camera.hasCursor == true && camera != null)
        {
            //New cursor found!
            lastCursor = camera.cursor.GetComponent<MonitorCursor>();
            Debug.Log(lastCursor.name + " is now the last cursor");
            //Enable cursorcontrol
            lastCursor.EnableCursorControl();
        }
    }

    private void DisableLastCursor()
    {
        // Make sure there's a cursor to disable
        if (lastCursor != null)
            lastCursor.DisableCursorControl();
    }

    public void PlaySound()
    {
        swoosh.pitch = Random.Range(.20f, .30f);
        swoosh.PlayOneShot(swoosh.clip);
    }
}
