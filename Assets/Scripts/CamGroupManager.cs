using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central manager that ensures only one CamGroup is active at a time.
/// Stores the currently selected group and applies UI state immediately or when maps become visible.
/// </summary>
public class CamGroupManager : MonoBehaviour
{
    private static CamGroupManager _instance;
    public static CamGroupManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing one
                _instance = FindObjectOfType<CamGroupManager>();
                if (_instance == null)
                {
                    // Create one automatically so callers don't need to ensure it exists in the scene
                    var go = new GameObject("CamGroupManager");
                    _instance = go.AddComponent<CamGroupManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    // currently active group (last one clicked). May be null.
    public CamGroup ActiveGroup { get; private set; }

    private void Awake()
    {
        // Ensure singleton instance (handles both manually placed and auto-created scenarios)
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Set the active group and apply it immediately where visible.
    /// If none of the group's camera UI GameObjects are currently active in the hierarchy
    /// we defer applying until visibility changes (SwapFloorsButton will reapply).
    /// </summary>
    public void SetActiveGroup(CamGroup group)
    {
        ActiveGroup = group;

        // If any camera UI for this group is currently visible in hierarchy, apply now.
        // Otherwise defer until the map becomes visible (avoids enabling multiple groups for hidden floors).
        if (IsAnyCameraUIVisible(group))
        {
            ApplyActiveGroupInternal();
        }
        else
        {
            Debug.Log($"[CamGroupManager] ActiveGroup set to '{group?.name ?? "null"}' but no cameras visible — deferring apply until visibility change.");
        }
    }

    /// <summary>
    /// Re-apply the currently active group (useful after swapping maps / enabling parents).
    /// Public entry that delegates to the internal implementation.
    /// </summary>
    public void ApplyActiveGroup()
    {
        ApplyActiveGroupInternal();
    }

    // internal implementation that does the actual work
    private void ApplyActiveGroupInternal()
    {
        // Debug trace
        //Debug.Log($"[CamGroupManager] Applying ActiveGroup = '{ActiveGroup?.name ?? "null"}'");

        // Ensure any zoomed camera is collapsed instantly so it cannot block new group's rendering
        var allCamImagesForCollapse = FindObjectsByType<CamImage>(FindObjectsSortMode.None);
        for (int c = 0; c < allCamImagesForCollapse.Length; c++)
        {
            var camImg = allCamImagesForCollapse[c];
            if (camImg == null) continue;
            if (camImg.IsZoomed())
                camImg.CollapseInstant();
        }

        // 1) Disable all CamImage UI entries (clean slate)
        var allCamImages = FindObjectsByType<CamImage>(FindObjectsSortMode.None);
        for (int i = 0; i < allCamImages.Length; i++)
        {
            var camImg = allCamImages[i];
            if (camImg == null) continue;
            var raw = camImg.GetComponent<RawImage>();
            if (raw != null) raw.enabled = false;
            var bc = camImg.GetComponent<BoxCollider2D>();
            if (bc != null) bc.enabled = false;
            if (camImg.titleText != null) camImg.titleText.enabled = false;
            if (camImg.descText != null) camImg.descText.enabled = false;
        }

        // 2) Deselect all groups
        var allGroups = FindObjectsByType<CamGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < allGroups.Length; i++)
        {
            if (allGroups[i] == null) continue;
            allGroups[i].Deselect();
        }

        // 3) If we have an active group, enable its entries (will be visible if map parent is active)
        if (ActiveGroup != null)
        {
            if (ActiveGroup.cameras != null)
            {
                for (int i = 0; i < ActiveGroup.cameras.Length; i++)
                {
                    var go = ActiveGroup.cameras[i];
                    if (go == null) continue;
                    var camImg = go.GetComponent<CamImage>();
                    if (camImg == null) continue;
                    var raw = camImg.GetComponent<RawImage>();
                    if (raw != null) raw.enabled = true;
                    var bc = camImg.GetComponent<BoxCollider2D>();
                    if (bc != null) bc.enabled = true;
                    if (camImg.titleText != null) camImg.titleText.enabled = true;
                    if (camImg.descText != null) camImg.descText.enabled = true;
                }
            }
            // set group visuals & lights & icons via the group's helper methods
            ActiveGroup.MakeSelected();
            ActiveGroup.ReplaceLights();
            ActiveGroup.ReplaceIcons();

            //Debug.Log($"[CamGroupManager] Activated group '{ActiveGroup.name}' and enabled {ActiveGroup.cameras?.Length ?? 0} camera slots.");
        }
    }

    // public helper to call from SwapFloorsButton after enabling a floor's cameras parent
    public void ReapplyActiveGroupForVisibility()
    {
        // When visibility changes (floor swap), ensure the previously selected group is applied now.
        ApplyActiveGroupInternal();
    }

    // Return true if any camera GameObject assigned to the group is activeInHierarchy now.
    private bool IsAnyCameraUIVisible(CamGroup group)
    {
        if (group == null || group.cameras == null) return false;
        for (int i = 0; i < group.cameras.Length; i++)
        {
            var go = group.cameras[i];
            if (go == null) continue;
            if (go.activeInHierarchy) return true;
        }
        return false;
    }
}