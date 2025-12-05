using System;
using UnityEngine;

/// <summary>
/// Painting task implemented without MonoBehaviour so it can be driven by TaskManager.
/// The TaskManager provides configuration via its inspector fields (only targets & distance). Per-painting overrides come from PaintingFallConfig.
/// </summary>
public class PaintingTask : ITask
{
    private string taskName = "PaintingTask";
    public override string TaskName => taskName;

    // defaults (used when PaintingFallConfig is absent or specific transforms are not set)
    private const float DefaultFallDepth = 1.4f;
    private const float DefaultFallForward = 0.5f;
    private const float DefaultFallRotationX = 90f;
    private const float DefaultFallDuration = 0.5f;
    private const float DefaultReturnDuration = 1.5f;

    // runtime state
    private Transform targetPainting;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 fallenPosition;
    private Quaternion fallenRotation;

    private bool isFalling = false;
    private bool hasFallen = false;
    private bool isReturning = false;
    private bool completed = false;

    private float fallTimer = 0f;
    private float returnTimer = 0f;

    private float fallDurationLocal;
    private float returnDurationLocal;

    public override void Initialize(TaskManager manager)
    {
        // store manager reference and pick up configured task name
        this.manager = manager;
        if (manager != null && !string.IsNullOrWhiteSpace(manager.PaintingTaskName))
            taskName = manager.PaintingTaskName;
        else
            taskName = "PaintingTask";
    }

    public override bool CanActivate(Transform player)
    {
        if (manager == null) return false;
        if (manager.paintingTargets == null || manager.paintingTargets.Count == 0) return false;
        if (player == null) return false;
        // we will activate only if at least one painting is far enough from player and not in the player's current room
        foreach (var t in manager.paintingTargets)
        {
            if (t == null) continue;
            float d = Vector3.Distance(player.position, t.position);
            if (d >= manager.paintingRequiredPlayerDistanceToFall && !IsPlayerInRoomForPainting(t, player))
                return true;
        }
        return false;
    }

    public override void Activate(Transform player)
    {
        // pick a random painting that is far enough and not in the player's room
        if (manager == null || manager.paintingTargets == null) return;

        var candidates = manager.paintingTargets.FindAll(t =>
            t != null &&
            Vector3.Distance(player.position, t.position) >= manager.paintingRequiredPlayerDistanceToFall &&
            !IsPlayerInRoomForPainting(t, player)
        );

        if (candidates == null || candidates.Count == 0)
        {
            completed = true; // nothing to do
            return;
        }

        targetPainting = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // capture original transform
        originalPosition = targetPainting.position;
        originalRotation = targetPainting.rotation;

        // read per-painting override if present
        var cfg = targetPainting.GetComponent<PaintingFallConfig>();

        // compute fallen position:
        // - Use local-space Vector3 offset from PaintingFallConfig converted to world via Transform.TransformVector.
        // - Otherwise, fallback to previous scalar defaults (forward along painting.forward and down).
        Vector3 offsetWorld;
        if (cfg != null)
        {
            offsetWorld = targetPainting.TransformVector(cfg.fallOffset);
        }
        else
        {
            offsetWorld = targetPainting.forward * DefaultFallForward + Vector3.down * DefaultFallDepth;
        }

        fallenPosition = originalPosition + offsetWorld;

        // compute fallen rotation
        if (cfg != null)
        {
            // apply local Euler rotation relative to the painting's original rotation
            fallenRotation = originalRotation * Quaternion.Euler(cfg.fallRotationEuler);
        }
        else
        {
            // fallback: rotate around local X by default angle so it falls face-first
            fallenRotation = originalRotation * Quaternion.Euler(DefaultFallRotationX, 0f, 0f);
        }

        // durations (allow per-painting override if > 0)
        fallDurationLocal = (cfg != null && cfg.fallDuration > 0f) ? cfg.fallDuration : DefaultFallDuration;
        returnDurationLocal = (cfg != null && cfg.returnDuration > 0f) ? cfg.returnDuration : DefaultReturnDuration;

        // start falling animation
        isFalling = true;
        fallTimer = 0f;
        hasFallen = false;
        isReturning = false;
        completed = false;
    }

    public override void Tick(Transform player)
    {
        if (completed || targetPainting == null) return;

        float dt = Time.deltaTime;

        if (isFalling)
        {
            fallTimer += dt;
            float f = Mathf.Clamp01(fallTimer / fallDurationLocal);
            targetPainting.position = Vector3.Lerp(originalPosition, fallenPosition, f);
            targetPainting.rotation = Quaternion.Slerp(originalRotation, fallenRotation, f);
            if (f >= 1f)
            {
                isFalling = false;
                hasFallen = true;
            }
            return;
        }

        if (isReturning)
        {
            returnTimer += dt;
            float f = Mathf.Clamp01(returnTimer / returnDurationLocal);
            targetPainting.position = Vector3.Lerp(fallenPosition, originalPosition, f);
            targetPainting.rotation = Quaternion.Slerp(targetPainting.rotation, originalRotation, f);
            if (f >= 1f)
            {
                isReturning = false;
                hasFallen = false;
                completed = true;
            }
            return;
        }

        // If painting is on floor, allow interaction when player close
        if (hasFallen)
        {
            float d = Vector3.Distance(player.position, targetPainting.position);

            // Simple interaction: press Interact when within range to return painting.
            if (d <= manager.paintingInteractionDistance && manager != null && manager.InteractPressed())
            {
                StartReturn();
            }
        }
    }

    private void StartReturn()
    {
        if (targetPainting == null) return;
        isReturning = true;
        returnTimer = 0f;
    }

    public override void Deactivate()
    {
        // if task cancelled mid-state, attempt to clean state (do not destroy objects)
        targetPainting = null;
    }

    public override bool IsCompleted => completed;

    // Helper: returns true if the painting has a PaintingFallConfig.roomBounds with a Collider
    // and the player's position is inside that collider bounds.
    private bool IsPlayerInRoomForPainting(Transform painting, Transform player)
    {
        if (painting == null || player == null) return false;
        var cfg = painting.GetComponent<PaintingFallConfig>();
        if (cfg == null || cfg.roomBounds == null) return false;
        var col = cfg.roomBounds.GetComponent<Collider>();
        if (col == null) return false;
        return col.bounds.Contains(player.position);
    }
}
