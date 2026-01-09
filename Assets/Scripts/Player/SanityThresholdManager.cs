using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages objects that should be enabled/spawned when sanity drops below specific thresholds.
/// Each entry in the list will enable its object once when sanity drops below the threshold.
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
    }

    [Header("Sanity Threshold Configuration")]
    [Tooltip("List of objects to enable at specific sanity thresholds. Objects will be enabled when sanity drops BELOW the threshold.")]
    public List<SanityThresholdEntry> thresholdEntries = new List<SanityThresholdEntry>();

    [Header("Debug")]
    [Tooltip("Enable debug logging for threshold triggers.")]
    public bool debugLog = false;

    private int lastSanityValue = 100;
    private Component playerStatsComponent;
    private System.Reflection.PropertyInfo sanityProperty;

    private void Start()
    {
        // Ensure all objects start disabled
        foreach (var entry in thresholdEntries)
        {
            if (entry.objectToEnable != null)
            {
                entry.objectToEnable.SetActive(false);
                entry.hasTriggered = false;
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

        // Get current sanity value
        int currentSanity = (int)sanityProperty.GetValue(playerStatsComponent, null);

        // Check each threshold entry
        foreach (var entry in thresholdEntries)
        {
            if (entry == null || entry.objectToEnable == null) continue;
            if (entry.hasTriggered) continue;

            // Trigger if sanity has dropped below threshold
            if (currentSanity < entry.sanityThreshold)
            {
                entry.objectToEnable.SetActive(true);
                entry.hasTriggered = true;

                if (debugLog)
                {
                    Debug.Log($"[SanityThresholdManager] Enabled '{entry.objectToEnable.name}' - Sanity dropped below {entry.sanityThreshold} (current: {currentSanity})");
                }
            }
        }

        lastSanityValue = currentSanity;
    }

    /// <summary>
    /// Reset all threshold states - useful when restarting the game/level.
    /// </summary>
    [ContextMenu("Reset All Thresholds")]
    public void ResetAllThresholds()
    {
        foreach (var entry in thresholdEntries)
        {
            if (entry == null) continue;
            entry.hasTriggered = false;
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
    /// </summary>
    public void TriggerThresholdByIndex(int index)
    {
        if (index < 0 || index >= thresholdEntries.Count) return;

        var entry = thresholdEntries[index];
        if (entry == null || entry.objectToEnable == null) return;

        entry.objectToEnable.SetActive(true);
        entry.hasTriggered = true;

        if (debugLog)
        {
            Debug.Log($"[SanityThresholdManager] Manually triggered threshold {index}: '{entry.objectToEnable.name}'");
        }
    }
}
