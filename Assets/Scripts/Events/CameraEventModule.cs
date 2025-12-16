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

    // Weight multiplier applied to the currently-selected group when choosing spawn group
    private const float SelectedGroupWeight = 6f;
    private const float DefaultGroupWeight = 1f;

    // fallback cooldown if EventManager's value is 0 or invalid
    private const float DefaultCameraEventCooldown = 120f;

    // New: explicit spawned flag requested
    private bool entitySpawned = false;

    public CameraEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        // initialize cooldown so we don't immediately spawn on scene load
        float configuredCd = (manager != null) ? manager.cameraEventCooldownSeconds : 0f;
        cooldownTimer = (configuredCd > 0f) ? configuredCd : DefaultCameraEventCooldown;

        happenedSinceBelowThreshold = false;
        entitySpawned = false;

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

        // decrement cooldown and detect expiry transition so we can reset the "happened" flag
        float oldCooldown = cooldownTimer;
        if (cooldownTimer > 0f)
            cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);

        if (oldCooldown > 0f && cooldownTimer <= 0f)
        {
            // cooldown finished -> allow the camera event again
            happenedSinceBelowThreshold = false;
        }

        // Reset the "happened" flag when sanity goes above threshold
        if (manager.playerSanity >= manager.cameraEventSanityThreshold)
            happenedSinceBelowThreshold = false;

        // If there's an active entity, ensure it gets destroyed when conditions no longer hold.
        if (activeEntity != null)
        {
            bool playerOnCameras = manager.playerManager != null && manager.playerManager.inInteractionView;
            bool groupStillSelected = entityGroup != null && entityGroup.selectedGroup;

            // If player left the camera UI, destroy the entity immediately.
            if (!playerOnCameras)
            {
                UnityEngine.Object.Destroy(activeEntity.gameObject);
                return;
            }
            // If the entity is visible but its group was deselected, destroy it.
            if (activeEntity.gameObject.activeSelf && !groupStillSelected)
            {
                UnityEngine.Object.Destroy(activeEntity.gameObject);
                return;
            }
            // If the entity is still hidden and the group just became selected, show it.
            if (!activeEntity.gameObject.activeSelf && groupStillSelected)
            {
                activeEntity.Show();
            }

            // while activeEntity exists we do not spawn another
            return;
        }

        // Replace all usages of Object.FindObjectsOfType<T>(bool) with Object.FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)
        // Fix for CS0618

        // In OnUpdate method:
        var existingEntities = UnityEngine.Object.FindObjectsByType<CameraEntity>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existingEntities != null && existingEntities.Length > 0)
        {
            if (activeEntity == null)
                activeEntity = existingEntities[0];
            entitySpawned = true;
            return;
        }

        // If module-level flag says an entity is already spawned, don't spawn
        if (entitySpawned)
            return;

        // Only attempt spawn when player is in camera interaction view
        bool playerOnCamerasNow = manager.playerManager != null && manager.playerManager.inInteractionView;
        if (!playerOnCamerasNow) return;

        // require sanity below threshold and not already happened while below threshold
        if (manager.playerSanity >= manager.cameraEventSanityThreshold) return;
        if (happenedSinceBelowThreshold) return;

        // Only attempt spawn if cooldown expired
        if (cooldownTimer > 0f) return;

        // compute a chance per second: use manager.baseChancePerSecond multiplied when on cameras
        float chancePerSecond = manager.baseChancePerSecond * manager.cameraEventChanceMultiplierOnCamera;

        // Use Poisson probability for per-frame chance: p = 1 - exp(-rate * dt)
        float rollThreshold = 1f - Mathf.Exp(-chancePerSecond * dt);

        if (UnityEngine.Random.value < rollThreshold)
            TryCreateEntity();
    }

    private void TryCreateEntity()
    {
        try
        {
            if (manager == null) return;
            if (camGroups == null || camGroups.Count == 0) return;

            // Safety: do not start a spawn if one is already active
            if (activeEntity != null) return;

            // In TryCreateEntity method:
            var existing = UnityEngine.Object.FindObjectsByType<CameraEntity>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0)
            {
                activeEntity = existing[0];
                entitySpawned = true;
                return;
            }

            // Prefer currently selected groups only (do not spawn on inactive groups)
            var selectedGroups = camGroups.FindAll(g => g != null && g.selectedGroup);
            if (selectedGroups == null || selectedGroups.Count == 0) return;

            // pick one of the selected groups at random
            CamGroup chosenGroup = selectedGroups[UnityEngine.Random.Range(0, selectedGroups.Count)];
            if (chosenGroup == null) return;

            // In TryCreateEntity method (for CameraSpot):
            var allSpots = UnityEngine.Object.FindObjectsByType<CameraSpot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var groupSpots = new System.Collections.Generic.List<CameraSpot>();
            foreach (var s in allSpots)
            {
                if (s == null) continue;
                if (s.camGroup == chosenGroup && s.spawnPoint != null)
                    groupSpots.Add(s);
            }
            if (groupSpots.Count == 0) return;

            var chosenSpot = groupSpots[UnityEngine.Random.Range(0, groupSpots.Count)];
            if (chosenSpot == null || chosenSpot.spawnPoint == null) return;
            if (manager.cameraEntityPrefab == null) return;

            // Instantiate entity
            var go = UnityEngine.Object.Instantiate(manager.cameraEntityPrefab, chosenSpot.spawnPoint.position, chosenSpot.spawnPoint.rotation, chosenSpot.spawnPoint);
            if (go == null) return;

            var entity = go.GetComponent<CameraEntity>();
            if (entity == null)
            {
                UnityEngine.Object.Destroy(go);
                return;
            }

            // assign reference before Show to avoid race
            activeEntity = entity;
            entityGroup = chosenGroup;

            // init, assign drain rate, subscribe, show immediately
            entity.Init(manager, chosenGroup);
            entity.sanityDrainPerSecond = manager.cameraEntitySanityDrainPerSecond;
            entity.onDestroyed += HandleEntityDestroyed;
            entity.Show();

            // start cooldown immediately
            float configuredCd = (manager != null) ? manager.cameraEventCooldownSeconds : 0f;
            cooldownTimer = (configuredCd > 0f) ? configuredCd : DefaultCameraEventCooldown;

            // mark that event happened while below threshold so we don't retrigger until cooldown/conditions reset
            happenedSinceBelowThreshold = true;
            entitySpawned = true;
        }
        catch (Exception)
        {
            // ensure we don't leave activeEntity referencing a broken object
            activeEntity = null;
            entitySpawned = false;
        }
    }

    // Handles entity destruction callback from CameraEntity
    private void HandleEntityDestroyed()
    {
        try
        {
            if (activeEntity != null)
                activeEntity.onDestroyed -= HandleEntityDestroyed;
        }
        catch { /* ignore */ }

        // start cooldown when the entity is removed (whether destroyed by module or self)
        float configuredCd = (manager != null) ? manager.cameraEventCooldownSeconds : 0f;
        cooldownTimer = (configuredCd > 0f) ? configuredCd : DefaultCameraEventCooldown;

        activeEntity = null;
        entityGroup = null;
        entitySpawned = false;
    }
}
