using UnityEngine;

/// <summary>
/// Per-painting fall configuration. Attach to painting GameObjects to override TaskManager defaults.
/// All offsets/rotations are local-space values applied relative to the painting's transform.
/// </summary>
[DisallowMultipleComponent]
public class PaintingFallConfig : MonoBehaviour
{
    [Tooltip("Local-space offset to apply when the painting has fallen. " +
             "Default (0, -1.4, 0.5) moves the painting 1.4 units down (local Y) and 0.5 units forward (local Z). " +
             "Use a single Vector3 to author combined forward/down offsets; Z is local forward.")]
    public Vector3 fallOffset = new Vector3(0f, -1.4f, 0.5f);

    [Tooltip("Local-space Euler rotation (degrees) to apply when the painting has fallen. " +
             "Default (90,0,0) rotates the painting 90° around its local X so it falls face-first. " +
             "You can set Z rotation here for paintings that rotate on Z when fallen.")]
    public Vector3 fallRotationEuler = new Vector3(90f, 0f, 0f);

    [Tooltip("Time it takes to animate the fall (seconds). Overrides TaskManager if set > 0).")]
    public float fallDuration = 0.5f;

    [Tooltip("Time it takes to return painting to original position (seconds). Overrides TaskManager if set > 0).")]
    public float returnDuration = 1.5f;

    [Header("Room blocking")]
    [Tooltip("Optional: Transform that represents the room bounds (center + scale as size). If set and the player is inside this room, the painting will NOT be chosen for the task.")]
    public Transform roomBounds;

    [Header("Runtime State (Auto-Saved)")]
    [Tooltip("Original position stored at first Awake - used for reset on game restart")]
    [SerializeField, HideInInspector] public Vector3 originalPosition;
    [Tooltip("Original rotation stored at first Awake - used for reset on game restart")]
    [SerializeField, HideInInspector] public Quaternion originalRotation;
    [Tooltip("Flag to track if original values have been initialized")]
    [SerializeField, HideInInspector] private bool hasInitializedOriginals = false;

    private void Awake()
    {
        // Only store original transform state on the FIRST Awake call (scene first load)
        // This prevents overwriting with fallen positions when the scene is reloaded
        if (!hasInitializedOriginals)
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            hasInitializedOriginals = true;
            Debug.Log($"[PaintingFallConfig] Initialized original position for '{name}': {originalPosition}");
        }
    }

    /// <summary>
    /// Reset the painting to its original position/rotation immediately.
    /// Called by TaskManager when restarting the game.
    /// </summary>
    public void ResetToOriginal()
    {
        if (hasInitializedOriginals)
        {
            transform.position = originalPosition;
            transform.rotation = originalRotation;
        }
    }
}
