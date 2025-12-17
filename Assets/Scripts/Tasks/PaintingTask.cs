using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Painting task implemented without MonoBehaviour so it can be driven by TaskManager.
/// The TaskManager provides configuration via its inspector fields (targets, distances, min/max fall count).
/// Per-painting overrides come from PaintingFallConfig.
/// 
/// Changed to support multiple paintings falling at the start of the night:
/// - Selects a random number of paintings between manager.paintingMinFallCount and manager.paintingMaxFallCount
/// - Spawns ALL selected paintings to fall simultaneously
/// - Task completes only when all fallen paintings have been returned by the player
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

    // Per-selected-painting runtime state
    private class PaintState
    {
        public Transform painting;
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public Vector3 fallenPosition;
        public Quaternion fallenRotation;
        public float fallDuration;
        public float returnDuration;

        public float fallTimer;
        public float returnTimer;

        public bool isFalling;
        public bool hasFallen;
        public bool isReturning;
        public bool completed;
    }

    private List<PaintState> states = new List<PaintState>();
    private bool completed = false;

    // Track how many chosen paintings still need to be returned before task completes
    private int remainingToReturn = 0;

    // store initial chosen count so UI can display total
    private int initialChosenCount = 0;

    // Expose read-only counts for UI
    public int RemainingToReturn => remainingToReturn;
    public int TotalChosen => initialChosenCount;

    public override void Initialize(TaskManager manager)
    {
        // store manager reference and pick up configured task name
        this.manager = manager;
        if (manager != null && !string.IsNullOrWhiteSpace(manager.PaintingTaskName))
            taskName = manager.PaintingTaskName;
        else
            taskName = "PaintingTask";
    }

    // Activation no longer requires a player object or distance/room checks.
    // If there are any configured paintingTargets (non-null) the painting task may run.
    public override bool CanActivate(Transform player)
    {
        if (manager == null) return false;
        if (manager.paintingTargets == null || manager.paintingTargets.Count == 0) return false;

        // If there's at least one non-null target, allow activation
        foreach (var t in manager.paintingTargets)
            if (t != null) return true;
        return false;
    }

    public override void Activate(Transform player)
    {
        // reset per-run state
        states.Clear();
        completed = false;
        remainingToReturn = 0;
        initialChosenCount = 0;

        if (manager == null || manager.paintingTargets == null)
        {
            completed = true;
            return;
        }

        // collect all non-null targets (no distance or room filtering)
        var candidates = new List<Transform>();
        foreach (var t in manager.paintingTargets)
        {
            if (t == null) continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[PaintingTask] Activate: no painting targets configured");
            completed = true;
            return;
        }

        // determine how many should fall this activation (clamped to available candidates)
        int pickCount;
        if (manager != null && manager.paintingForceAllToFall)
        {
            // force all configured candidates to fall
            pickCount = candidates.Count;
        }
        else
        {
            int minCount = Mathf.Max(1, manager.paintingMinFallCount);
            int maxCount = Mathf.Max(minCount, manager.paintingMaxFallCount);
            pickCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            pickCount = Mathf.Clamp(pickCount, 1, candidates.Count);
        }

        Debug.Log($"[PaintingTask] Activating: picking {pickCount} paintings to fall (candidates={candidates.Count})");

        // pick unique random painting transforms
        var chosen = new List<Transform>();
        var pool = new List<Transform>(candidates);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            chosen.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        // log chosen names/positions for debugging
        foreach (var c in chosen)
        {
            if (c != null)
                Debug.Log($"[PaintingTask] Chosen painting: {c.name} at {c.position}");
        }

        // create state entries for each chosen painting
        foreach (var p in chosen)
        {
            if (p == null) continue;
            var s = new PaintState();
            s.painting = p;
            s.originalPosition = p.position;
            s.originalRotation = p.rotation;

            var cfg = p.GetComponent<PaintingFallConfig>();

            // compute fallen position (use local offset if provided)
            Vector3 offsetWorld;
            if (cfg != null)
                offsetWorld = p.TransformVector(cfg.fallOffset);
            else
                offsetWorld = p.forward * DefaultFallForward + Vector3.down * DefaultFallDepth;

            s.fallenPosition = s.originalPosition + offsetWorld;

            // compute fallen rotation
            if (cfg != null)
                s.fallenRotation = s.originalRotation * Quaternion.Euler(cfg.fallRotationEuler);
            else
                s.fallenRotation = s.originalRotation * Quaternion.Euler(DefaultFallRotationX, 0f, 0f);

            s.fallDuration = (cfg != null && cfg.fallDuration > 0f) ? cfg.fallDuration : DefaultFallDuration;
            s.returnDuration = (cfg != null && cfg.returnDuration > 0f) ? cfg.returnDuration : DefaultReturnDuration;

            s.fallTimer = 0f;
            s.returnTimer = 0f;
            s.isFalling = true;
            s.hasFallen = false;
            s.isReturning = false;
            s.completed = false;

            states.Add(s);
        }

        // remainingToReturn equals number of chosen paintings that must be returned
        remainingToReturn = states.Count;

        // record initial count for UI
        initialChosenCount = states.Count;

        // notify UI that task started / counts changed
        manager?.NotifyTasksChanged();

        // if nothing created, mark completed
        if (states.Count == 0)
        {
            Debug.Log("[PaintingTask] No states created; marking completed");
            completed = true;
            manager?.NotifyTasksChanged();
        }
    }

    public override void Tick(Transform player)
    {
        if (completed || states == null || states.Count == 0) return;

        float dt = Time.deltaTime;

        // Process each painting's animation & interaction independently
        for (int i = 0; i < states.Count; i++)
        {
            var s = states[i];
            if (s == null || s.painting == null) continue;

            // Falling animation
            if (s.isFalling)
            {
                s.fallTimer += dt;
                float f = Mathf.Clamp01(s.fallTimer / Mathf.Max(0.0001f, s.fallDuration));
                s.painting.position = Vector3.Lerp(s.originalPosition, s.fallenPosition, f);
                s.painting.rotation = Quaternion.Slerp(s.originalRotation, s.fallenRotation, f);
                if (f >= 1f)
                {
                    s.isFalling = false;
                    s.hasFallen = true;
                    Debug.Log($"[PaintingTask] Painting {s.painting.name} has fallen");
                }
                continue;
            }

            // Returning animation
            if (s.isReturning)
            {
                s.returnTimer += dt;
                float f = Mathf.Clamp01(s.returnTimer / Mathf.Max(0.0001f, s.returnDuration));
                s.painting.position = Vector3.Lerp(s.fallenPosition, s.originalPosition, f);
                s.painting.rotation = Quaternion.Slerp(s.painting.rotation, s.originalRotation, f);
                if (f >= 1f)
                {
                    s.isReturning = false;
                    s.hasFallen = false;
                    if (!s.completed)
                    {
                        s.completed = true;
                        remainingToReturn = Mathf.Max(0, remainingToReturn - 1);
                        Debug.Log($"[PaintingTask] Painting {s.painting.name} returned and completed; remainingToReturn={remainingToReturn}");
                        // notify UI of progress change
                        manager?.NotifyTasksChanged();
                    }
                }
                continue;
            }

            // If painting has fallen, allow player to interact to return it
            if (s.hasFallen)
            {
                // Use interaction distance from manager; player must be present for interaction checks
                if (player == null) continue;
                float d = Vector3.Distance(player.position, s.painting.position);
                if (d <= manager.paintingInteractionDistance && manager != null && manager.InteractPressed())
                {
                    s.isReturning = true;
                    s.returnTimer = 0f;
                }
            }
        }

        // Complete only when all chosen (fallen) paintings have been returned
        if (remainingToReturn <= 0)
        {
            completed = true;
            Debug.Log("[PaintingTask] All chosen paintings returned - task completed");
            manager?.NotifyTasksChanged();
        }
    }

    public override void Deactivate()
    {
        // Reset runtime state. We do not forcibly teleport paintings back to avoid interfering
        // with gameplay; Deactivate simply abandons the task state and leaves scene objects as-is.
        states.Clear();
        completed = false;
        remainingToReturn = 0;
        initialChosenCount = 0;
        manager?.NotifyTasksChanged();
    }

    public override bool IsCompleted => completed;
}