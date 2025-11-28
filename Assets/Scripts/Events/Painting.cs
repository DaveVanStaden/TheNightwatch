using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Painting : MonoBehaviour
{
    [Tooltip("Renderer that holds the material (leave empty to auto-find).")]
    public Renderer targetRenderer;

    [Tooltip("Original / normal material for the painting.")]
    public Material originalMaterial;

    [Tooltip("Distorted material to switch to.")]
    public Material distortedMaterial;

    // runtime state
    [HideInInspector] public bool IsDistorted { get; private set; }

    // For renderers that have multiple materials (SkinnedMeshRenderer) we store which index we replace.
    private int targetMaterialIndex = -1;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        // If originalMaterial isn't set explicitly, capture current material at Awake.
        if (targetRenderer != null)
        {
            // Always prefer the second shared material (index 1) when present and store it as the original.
            var shared = targetRenderer.sharedMaterials;
            if (shared != null && shared.Length > 1)
            {
                targetMaterialIndex = 1;               // force second material
                originalMaterial = shared[1];
                if (originalMaterial == null)
                    Debug.LogWarning($"[Painting] '{gameObject.name}' original material at index 1 is null.");
            }
            else
            {
                // Fall back to single-material behavior (no index-1 available)
                targetMaterialIndex = -1;
                originalMaterial = targetRenderer.sharedMaterial;
                if (shared == null || shared.Length <= 1)
                    Debug.LogWarning($"[Painting] '{gameObject.name}' does not have a second material (index 1). Falling back to single-material behavior.");
            }
        }
    }

    /// <summary>
    /// Apply the distorted material immediately.
    /// Replaces the material at the previously selected index for multi-material renderers.
    /// </summary>
    // Replace ApplyDistortion() body with this guarded version that ONLY replaces index 1 (never index 0).
    public void ApplyDistortion()
    {
        if (targetRenderer == null || distortedMaterial == null)
        {
            Debug.LogWarning($"[Painting] Cannot distort '{gameObject.name}': missing renderer or distortedMaterial.");
            return;
        }

        // Work on the renderer.materials array (this creates instances where needed)
        var mats = targetRenderer.materials;
        if (mats != null && mats.Length > 1)
        {
            // Ensure we always target index 1 for the artwork
            int idx = 1;

            // Cache original material at index 1 if not already stored
            if (originalMaterial == null && idx < mats.Length)
                originalMaterial = targetRenderer.sharedMaterials != null && targetRenderer.sharedMaterials.Length > idx
                    ? targetRenderer.sharedMaterials[idx]
                    : mats[idx];

            // Apply distortion
            mats[idx] = distortedMaterial;
            targetRenderer.materials = mats;
            IsDistorted = true;
            Debug.Log($"[Painting] Applied distortion to '{gameObject.name}' on material index {idx}.");
            return;
        }

        // Fallback: single-material renderer (no index 1 available) — apply only if explicitly desired
        if (mats != null && mats.Length == 1)
        {
            if (originalMaterial == null)
                originalMaterial = targetRenderer.sharedMaterial;

            targetRenderer.material = distortedMaterial;
            IsDistorted = true;
            Debug.Log($"[Painting] Applied distortion to '{gameObject.name}' (single material fallback).");
            return;
        }

        Debug.LogWarning($"[Painting] '{gameObject.name}' has no suitable material slot to apply distortion (needs at least 2 materials).");
    }

    /// <summary>
    /// Revert to the original material immediately.
    /// Restores the material at the saved index for multi-material renderers.
    /// </summary>
    // Replace RevertToOriginal() body with this guarded version that ONLY restores index 1 (never index 0).
    public void RevertToOriginal()
    {
        if (targetRenderer == null || originalMaterial == null)
        {
            Debug.LogWarning($"[Painting] Cannot revert '{gameObject.name}': missing renderer or originalMaterial.");
            return;
        }

        var mats = targetRenderer.materials;
        if (mats != null && mats.Length > 1)
        {
            int idx = 1;
            mats[idx] = originalMaterial;
            targetRenderer.materials = mats;
            IsDistorted = false;
            Debug.Log($"[Painting] Reverted '{gameObject.name}' to original material at index {idx}.");
            return;
        }

        // Fallback: single-material renderer
        if (mats != null && mats.Length == 1)
        {
            targetRenderer.material = originalMaterial;
            IsDistorted = false;
            Debug.Log($"[Painting] Reverted '{gameObject.name}' to original (single material fallback).");
            return;
        }

        Debug.LogWarning($"[Painting] '{gameObject.name}' has no suitable material slot to revert.");
    }

    /// <summary>
    /// Returns true when the provided camera is looking at this painting.
    /// Uses a maxAngle check and a raycast to ensure it's not occluded.
    /// </summary>
    public bool IsPlayerLooking(Camera cam, float maxAngleDegrees = 40f)
    {
        if (cam == null || targetRenderer == null) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 toCenter = targetRenderer.bounds.center - camPos;
        float distance = toCenter.magnitude;
        if (distance <= 0.001f) return false;

        float angle = Vector3.Angle(cam.transform.forward, toCenter);
        if (angle > maxAngleDegrees) return false;

        // Raycast to check occlusion
        Ray ray = new Ray(camPos, toCenter.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, distance + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            // Hit the painting's collider (or a child)
            var paintingCollider = GetComponent<Collider>();
            if (paintingCollider != null && hit.collider == paintingCollider) return true;

            // allow if hit object is part of the same root (renderer may be on child)
            if (hit.collider.transform.IsChildOf(transform) || transform.IsChildOf(hit.collider.transform)) return true;

            return false;
        }

        // no hit - assume not visible
        return false;
    }

    // Helper to remove Unity's " (Instance)" suffix from material names
    private string NormalizeMaterialName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        name = name.Trim();
        const string suffixA = " (Instance)";
        const string suffixB = "(Instance)";
        if (name.EndsWith(suffixA))
            name = name.Substring(0, name.Length - suffixA.Length);
        if (name.EndsWith(suffixB))
            name = name.Substring(0, name.Length - suffixB.Length);
        return name.Trim();
    }
}
