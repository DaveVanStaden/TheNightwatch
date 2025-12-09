using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event module that spawns a CameraEntity attached to a random camera in a random CamGroup.
/// Uses EventManager.allCamGroups (populated by the MonoBehaviour) instead of FindObjectsOfType.
/// </summary>
public class CameraEventModule : IEventModule
{
    private float cooldownTimer = 0f;
    private bool happenedSinceBelowThreshold = false;
    private CameraEntity activeEntity = null;
    private CamGroup entityGroup = null;
    private List<CamGroup> camGroups = new List<CamGroup>();

    public CameraEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        cooldownTimer = 0f;
        happenedSinceBelowThreshold = false;

        // read the list provided by EventManager (EventManager is a MonoBehaviour so it can call FindObjectsOfType)
        if (manager != null && manager.allCamGroups != null)
            camGroups = new List<CamGroup>(manager.allCamGroups);
        else
            camGroups = new List<CamGroup>();
    }

    public override void OnStart()
    {
        // nothing to do
    }

    public override void OnUpdate()
    {
        if (manager == null) return;

        float dt = Time.deltaTime;

        if (cooldownTimer > 0f) cooldownTimer -= dt;

        // Reset the "happened" flag when sanity goes above threshold
        if (manager.playerSanity >= manager.cameraEventSanityThreshold)
            happenedSinceBelowThreshold = false;

        // If there's an active entity, ensure it gets destroyed when conditions no longer hold
        if (activeEntity != null)
        {
            bool playerOnCameras = manager.playerManager != null && manager.playerManager.inInteractionView;
            bool groupStillSelected = entityGroup != null && entityGroup.selectedGroup;

            if (!playerOnCameras || !groupStillSelected)
            {
                // destroy the entity
                UnityEngine.Object.Destroy(activeEntity.gameObject);
                activeEntity = null;
                entityGroup = null;
            }
            else
            {
                // If entity exists but not yet shown & its group just became selected, show it
                if (!activeEntity.gameObject.activeSelf && groupStillSelected)
                {
                    activeEntity.Show();
                }
            }

            // while activeEntity exists we do not spawn another
            return;
        }

        // Only attempt spawn when player is in camera interaction view and has the cameras angle open (angle == 2)
        bool playerOnCamerasNow = manager.playerManager != null && manager.playerManager.inInteractionView;

        if (!playerOnCamerasNow) return;

        // find current active Interactable (interaction camera enabled)
        Interactable activeInteract = null;
        var list = UnityEngine.Object.FindObjectsOfType<Interactable>();
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i].interactionCamera != null && list[i].interactionCamera.enabled)
            {
                activeInteract = list[i];
                break;
            }
        }

        if (activeInteract == null) return;

        // require angle == 2 to be looking at camera UI
        if (activeInteract.currentAngle != 2) return;

        // require sanity below threshold and not already happened while below threshold
        if (manager.playerSanity >= manager.cameraEventSanityThreshold) return;
        if (happenedSinceBelowThreshold) return;

        // Only attempt spawn if cooldown expired
        if (cooldownTimer > 0f) return;

        // compute a chance per second: use manager.baseChancePerSecond multiplied when on cameras
        float chancePerSecond = manager.baseChancePerSecond * manager.cameraEventChanceMultiplierOnCamera;
        float roll = chancePerSecond * dt;
        if (UnityEngine.Random.value < roll)
        {
            TryCreateEntity();
        }
    }

    private void TryCreateEntity()
    {
        if (camGroups == null || camGroups.Count == 0)
        {
            Debug.LogWarning("[CameraEventModule] No CamGroup found in manager.allCamGroups.");
            return;
        }

        // pick a random CamGroup
        CamGroup chosenGroup = camGroups[UnityEngine.Random.Range(0, camGroups.Count)];
        if (chosenGroup == null || chosenGroup.cameras == null || chosenGroup.cameras.Length == 0)
        {
            Debug.LogWarning("[CameraEventModule] Chosen group has no cameras.");
            return;
        }

        // pick one of the 4 cameras in the group
        int camIndex = UnityEngine.Random.Range(0, Mathf.Min(4, chosenGroup.cameras.Length));
        GameObject camView = chosenGroup.cameras[camIndex];
        if (camView == null)
        {
            Debug.LogWarning("[CameraEventModule] Selected camera view is null.");
            return;
        }

        // Get CamImage to find the originalCam (SecurityCamera)
        CamImage camImage = camView.GetComponent<CamImage>();
        if (camImage == null || camImage.originalCam == null)
        {
            Debug.LogWarning("[CameraEventModule] CamImage or originalCam missing on camera view.");
            return;
        }

        SecurityCamera secCam = camImage.originalCam;
        CameraSpot spot = secCam.GetComponent<CameraSpot>();
        if (spot == null || spot.spawnPoint == null)
        {
            Debug.LogWarning("[CameraEventModule] CameraSpot/spawnPoint missing on SecurityCamera: " + secCam.name);
            return;
        }

        if (manager.cameraEntityPrefab == null)
        {
            Debug.LogWarning("[CameraEventModule] cameraEntityPrefab not assigned on EventManager.");
            return;
        }

        // Instantiate but keep inactive until group is selected
        GameObject go = UnityEngine.Object.Instantiate(manager.cameraEntityPrefab, spot.spawnPoint.position, spot.spawnPoint.rotation, spot.spawnPoint);
        var entity = go.GetComponent<CameraEntity>();
        if (entity == null)
        {
            Debug.LogWarning("[CameraEventModule] cameraEntityPrefab doesn't have CameraEntity component.");
            UnityEngine.Object.Destroy(go);
            return;
        }

        entity.Init(manager, chosenGroup);

        // keep reference and set cooldown + happened flag
        activeEntity = entity;
        entityGroup = chosenGroup;
        cooldownTimer = manager.cameraEventCooldownSeconds;
        happenedSinceBelowThreshold = true;

        Debug.Log($"[CameraEventModule] Spawned hidden CameraEntity on group {chosenGroup.name}, cam index {camIndex}. Waiting for group selection to show it.");
    }
}
