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

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        // If originalMaterial isn't set explicitly, capture current material at Awake.
        if (targetRenderer != null && originalMaterial == null)
        {
            originalMaterial = targetRenderer.sharedMaterial;
        }
    }

    /// <summary>
    /// Apply the distorted material immediately.
    /// </summary>
    public void ApplyDistortion()
    {
        if (targetRenderer == null || distortedMaterial == null)
        {
            Debug.LogWarning($"[Painting] Cannot distort '{gameObject.name}': missing renderer or distortedMaterial.");
            return;
        }
        targetRenderer.material = distortedMaterial;
        IsDistorted = true;
        Debug.Log($"[Painting] Applied distortion to '{gameObject.name}'.");
    }

    /// <summary>
    /// Revert to the original material immediately.
    /// </summary>
    public void RevertToOriginal()
    {
        if (targetRenderer == null || originalMaterial == null)
        {
            Debug.LogWarning($"[Painting] Cannot revert '{gameObject.name}': missing renderer or originalMaterial.");
            return;
        }
        targetRenderer.material = originalMaterial;
        IsDistorted = false;
        Debug.Log($"[Painting] Reverted '{gameObject.name}' to original.");
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
}
