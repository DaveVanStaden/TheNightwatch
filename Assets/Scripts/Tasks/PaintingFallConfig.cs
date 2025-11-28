using UnityEngine;

/// <summary>
/// Per-painting fall configuration. Attach to painting GameObjects to override TaskManager defaults.
/// </summary>
[DisallowMultipleComponent]
public class PaintingFallConfig : MonoBehaviour
{
    [Tooltip("How far the painting falls (world units down).")]
    public float fallDepth = 1.4f;

    [Tooltip("How far forward (local forward) the painting moves when it falls.")]
    public float fallForward = 0.5f;

    [Tooltip("Rotation angle (degrees) around local X applied when painting has fallen (face-first).")]
    public float fallRotationX = 90f;

    [Tooltip("Time it takes to animate the fall (seconds). Overrides TaskManager if set > 0).")]
    public float fallDuration = 0.5f;

    [Tooltip("Time it takes to return painting to original position (seconds). Overrides TaskManager if set > 0).")]
    public float returnDuration = 1.5f;

    [Header("Room blocking")]
    [Tooltip("Optional: Transform that represents the room bounds (center + scale as size). If set and the player is inside this room, the painting will NOT be chosen for the task.")]
    public Transform roomBounds;
}
