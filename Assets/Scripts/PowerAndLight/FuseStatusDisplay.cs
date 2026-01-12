using System.Collections.Generic;
using UnityEngine;

public class FuseStatusDisplay : MonoBehaviour
{
    [System.Serializable]
    public class FuseDisplayEntry
    {
        [Tooltip("The breaker button to monitor (e.g., Fuse A, Fuse B, etc.)")]
        public BreakerButton breakerButton;

        [Tooltip("The TextMeshPro component on the PC display for this fuse")]
        public TMPro.TextMeshProUGUI displayText;

        [HideInInspector]
        public bool lastKnownState;
    }

    [Header("Fuse Display Entries")]
    [Tooltip("List of fuses to monitor and their corresponding PC display text")]
    public List<FuseDisplayEntry> fuseEntries = new List<FuseDisplayEntry>();

    [Header("Colors")]
    [Tooltip("Color when fuse is ON (green)")]
    public Color onColor = Color.green;

    [Tooltip("Color when fuse is OFF (red)")]
    public Color offColor = Color.red;

    [Header("Settings")]
    [Tooltip("How often to check fuse states (in seconds). Lower = more responsive but higher performance cost.")]
    [Range(0.1f, 1f)]
    public float updateInterval = 0.2f;

    private float updateTimer;

    private void Start()
    {
        foreach (var entry in fuseEntries)
        {
            if (entry.breakerButton != null)
            {
                bool isOn = entry.breakerButton.isOn;
                entry.lastKnownState = isOn;
                UpdateDisplayColor(entry, isOn);
            }
        }

        updateTimer = updateInterval;
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;
        if (updateTimer <= 0f)
        {
            updateTimer = updateInterval;
            CheckAndUpdateFuses();
        }
    }

    private void CheckAndUpdateFuses()
    {
        foreach (var entry in fuseEntries)
        {
            if (entry.breakerButton == null || entry.displayText == null) continue;

            bool currentState = entry.breakerButton.isOn;

            if (currentState != entry.lastKnownState)
            {
                entry.lastKnownState = currentState;
                UpdateDisplayColor(entry, currentState);
            }
        }
    }

    private void UpdateDisplayColor(FuseDisplayEntry entry, bool isOn)
    {
        if (entry.displayText == null) return;

        Color targetColor = isOn ? onColor : offColor;
        entry.displayText.color = targetColor;
    }
}
