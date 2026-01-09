using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages objects that should be enabled/spawned when sanity drops below specific thresholds.
/// Each entry in the list will enable its object once when sanity drops below the threshold.
/// Objects won't spawn within a configurable radius of the player to prevent visible pop-in.
/// </summary>
public class SanityThresholdManager : MonoBehaviour
{
    [System.Serializable]
    public class SanityThresholdEntry
    {
        [Tooltip("The GameObject to enable when sanity drops below the threshold.")]
        public GameObject objectToEnable;

        [Tooltip("Sanity threshold - object will be enabled when sanity drops below this value.")]
        [Range(0, 100)]
        public int sanityThreshold = 50;

        [Tooltip("Has this threshold been triggered already? (runtime state)")]
        [HideInInspector]
        public bool hasTriggered = false;

        [Tooltip("Is this entry currently waiting to spawn? (runtime state)")]
        [HideInInspector]
        public bool isWaitingToSpawn = false;
    }

    [Header("Sanity Threshold Configuration")]
    [Tooltip("List of objects to enable at specific sanity thresholds. Objects will be enabled when sanity drops BELOW the threshold.")]
    public List<SanityThresholdEntry> thresholdEntries = new List<SanityThresholdEntry>();

    [Header("Spawn Prevention Settings")]
    [Tooltip("Objects won't spawn if they're within this distance from the player (prevents visible pop-in).")]
    [Range(0f, 50f)]
    public float minSpawnDistance = 45f;

    [Tooltip("Random delay range (min seconds) before spawning the object. Gives player time to look away naturally.")]
    [Range(0f, 5f)]
    public float minSpawnDelay = 0.5f;

    [Tooltip("Random delay range (max seconds) before spawning the object.")]
    [Range(0f, 5f)]
    public float maxSpawnDelay = 2f;

    [Tooltip("How often (in seconds) to check if a waiting object can spawn. Lower = more responsive but higher performance cost.")]
    [Range(0.1f, 2f)]
    public float spawnCheckInterval = 0.5f;

    [Header("Debug")]
    [Tooltip("Enable debug logging for threshold triggers.")]
    public bool debugLog = false;

    [Tooltip("Show spawn radius in Scene view (editor only).")]
    public bool showSpawnRadiusGizmo = true;

    private int lastSanityValue = 100;
    private Component playerStatsComponent;
    private System.Reflection.PropertyInfo sanityProperty;
    private Transform playerTransform;

    private void Start()
    {
        // Find player transform
        playerTransform = FindPlayerTransform();

        // Ensure all objects start disabled
        foreach (var entry in thresholdEntries)
        {
            if (entry.objectToEnable != null)
            {
                entry.objectToEnable.SetActive(false);
                entry.hasTriggered = false;
                entry.isWaitingToSpawn = false;
            }
        }

        // Find PlayerStats and cache the sanity property for performance
        var playerStatsType = System.Type.GetType("PlayerStats");
        if (playerStatsType != null)
        {
            // Try to get Instance property first (more efficient)
            var instanceProp = playerStatsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp != null)
            {
                playerStatsComponent = instanceProp.GetValue(null, null) as Component;
            }
            else
            {
                // Fallback to FindObjectOfType
                playerStatsComponent = FindObjectOfType(playerStatsType) as Component;
            }

            if (playerStatsComponent != null)
            {
                sanityProperty = playerStatsType.GetProperty("Sanity");
                if (sanityProperty != null)
                {
                    lastSanityValue = (int)sanityProperty.GetValue(playerStatsComponent, null);
                    if (debugLog)
                    {
                        Debug.Log($"[SanityThresholdManager] Initialized with sanity: {lastSanityValue}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[SanityThresholdManager] PlayerStats component not found in scene!");
            }
        }
        else
        {
            Debug.LogError("[SanityThresholdManager] PlayerStats type not found!");
        }
    }

    private void Update()
    {
        if (playerStatsComponent == null || sanityProperty == null) return;

        // Update player transform reference if it was null
        if (playerTransform == null)
        {
            playerTransform = FindPlayerTransform();
        }

        // Get current sanity value
        int currentSanity = (int)sanityProperty.GetValue(playerStatsComponent, null);

        // Check each threshold entry
        foreach (var entry in thresholdEntries)
        {
            if (entry == null || entry.objectToEnable == null) continue;
            if (entry.hasTriggered || entry.isWaitingToSpawn) continue;

            // Trigger if sanity has dropped below threshold
            if (currentSanity < entry.sanityThreshold)
            {
                // Start the spawn process (with distance check and delay)
                StartCoroutine(TrySpawnObject(entry));
            }
        }

        lastSanityValue = currentSanity;
    }

    /// <summary>
    /// Attempts to spawn an object, checking distance and applying delay.
    /// Waits until the object is far enough from the player before enabling it.
    /// </summary>
    private IEnumerator TrySpawnObject(SanityThresholdEntry entry)
    {
        entry.isWaitingToSpawn = true;

        if (debugLog)
        {
            Debug.Log($"[SanityThresholdManager] Threshold met for '{entry.objectToEnable.name}' - waiting to spawn...");
        }

        // Apply random initial delay
        float delay = UnityEngine.Random.Range(minSpawnDelay, maxSpawnDelay);
        yield return new WaitForSeconds(delay);

        // Wait until object is far enough from player
        while (playerTransform != null && IsObjectTooCloseToPlayer(entry.objectToEnable))
        {
            if (debugLog)
            {
                float distance = Vector3.Distance(playerTransform.position, entry.objectToEnable.transform.position);
                Debug.Log($"[SanityThresholdManager] Object '{entry.objectToEnable.name}' too close to player ({distance:F1}m) - waiting...");
            }

            yield return new WaitForSeconds(spawnCheckInterval);
        }

        // Finally enable the object
        entry.objectToEnable.SetActive(true);
        entry.hasTriggered = true;
        entry.isWaitingToSpawn = false;

        if (debugLog)
        {
            Debug.Log($"[SanityThresholdManager] Enabled '{entry.objectToEnable.name}' - Sanity dropped below {entry.sanityThreshold}");
        }
    }

    /// <summary>
    /// Checks if an object is within the minimum spawn distance from the player.
    /// </summary>
    private bool IsObjectTooCloseToPlayer(GameObject obj)
    {
        if (playerTransform == null || obj == null) return false;

        float distance = Vector3.Distance(playerTransform.position, obj.transform.position);
        return distance < minSpawnDistance;
    }

    /// <summary>
    /// Finds the player transform in the scene.
    /// </summary>
    private Transform FindPlayerTransform()
    {
        // Try to find PlayerManager first
        var playerManagerType = System.Type.GetType("PlayerManager");
        if (playerManagerType != null)
        {
            var playerManager = FindObjectOfType(playerManagerType) as Component;
            if (playerManager != null)
            {
                return playerManager.transform;
            }
        }

        // Fallback: try to find by tag
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            return playerObj.transform;
        }

        // Last resort: use PlayerStats transform
        if (playerStatsComponent != null)
        {
            return playerStatsComponent.transform;
        }

        Debug.LogWarning("[SanityThresholdManager] Could not find player transform!");
        return null;
    }

    /// <summary>
    /// Reset all threshold states - useful when restarting the game/level.
    /// </summary>
    [ContextMenu("Reset All Thresholds")]
    public void ResetAllThresholds()
    {
        // Stop all spawning coroutines
        StopAllCoroutines();

        foreach (var entry in thresholdEntries)
        {
            if (entry == null) continue;
            entry.hasTriggered = false;
            entry.isWaitingToSpawn = false;
            if (entry.objectToEnable != null)
            {
                entry.objectToEnable.SetActive(false);
            }
        }

        if (debugLog)
        {
            Debug.Log("[SanityThresholdManager] All thresholds reset");
        }
    }

    /// <summary>
    /// Manually trigger a specific threshold by index (for testing).
    /// Bypasses distance checks and delay.
    /// </summary>
    public void TriggerThresholdByIndex(int index)
    {
        if (index < 0 || index >= thresholdEntries.Count) return;

        var entry = thresholdEntries[index];
        if (entry == null || entry.objectToEnable == null) return;

        entry.objectToEnable.SetActive(true);
        entry.hasTriggered = true;
        entry.isWaitingToSpawn = false;

        if (debugLog)
        {
            Debug.Log($"[SanityThresholdManager] Manually triggered threshold {index}: '{entry.objectToEnable.name}' (bypassed distance check)");
        }
    }

    /// <summary>
    /// Force spawn all waiting objects immediately (useful for testing).
    /// </summary>
    [ContextMenu("Force Spawn All Waiting Objects")]
    public void ForceSpawnAllWaitingObjects()
    {
        StopAllCoroutines();

        foreach (var entry in thresholdEntries)
        {
            if (entry == null || entry.objectToEnable == null) continue;
            if (entry.isWaitingToSpawn && !entry.hasTriggered)
            {
                entry.objectToEnable.SetActive(true);
                entry.hasTriggered = true;
                entry.isWaitingToSpawn = false;

                if (debugLog)
                {
                    Debug.Log($"[SanityThresholdManager] Force-spawned '{entry.objectToEnable.name}'");
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showSpawnRadiusGizmo || playerTransform == null) return;

        // Draw spawn radius around player
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(playerTransform.position, minSpawnDistance);

        // Draw lines to objects that are waiting to spawn
        Gizmos.color = Color.yellow;
        foreach (var entry in thresholdEntries)
        {
            if (entry == null || entry.objectToEnable == null) continue;
            if (entry.isWaitingToSpawn)
            {
                Gizmos.DrawLine(playerTransform.position, entry.objectToEnable.transform.position);
                Gizmos.DrawWireSphere(entry.objectToEnable.transform.position, 1f);
            }
        }
    }
#endif
}
