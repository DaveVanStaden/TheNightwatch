using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mini task: a single, specific painting falls when the player is zoomed-in on the selected CamImage
/// AND an internal phone/timer has elapsed (or phone gating via TaskManager). After the painting
/// is returned the task completes and (optionally) plays a completion clip via the TaskManager's specialPhoneAudioSource.
/// Targets a single static painting Transform assigned on the TaskManager.
/// </summary>
public class SinglePaintingFallTask : ITask
{
    private string taskName = "SinglePaintingFallTask";
    public override string TaskName => taskName;

    // small state for the single painting
    private class State
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

    private State state;
    private bool completed = false;

    // phone timer / gating (kept internal to the task)
    private float phoneTimer = 0f;
    private bool phoneAllowed = false;

    // configuration defaults
    private const float DefaultFallDepth = 1.4f;
    private const float DefaultFallForward = 0.5f;
    private const float DefaultFallRotationX = 90f;
    private const float DefaultFallDuration = 0.6f;
    private const float DefaultReturnDuration = 1.5f;
    private const float DefaultPhoneDelay = 10f;

    // manager reference filled in Initialize
    private TaskManager manager;

    // runtime config read from manager
    private float phoneDelay = DefaultPhoneDelay;

    public override void Initialize(TaskManager manager)
    {
        this.manager = manager;
        if (manager != null)
        {
            taskName = "SinglePaintingFall";
            phoneDelay = Mathf.Max(0f, manager.specialPhoneDelay);
        }
    }

    public override bool CanActivate(Transform player)
    {
        if (manager == null) return false;
        // only activate if a special painting target is assigned and it exists in scene,
        // and task has not completed previously.
        return manager.specialPaintingTarget != null && state == null && !completed;
    }

    public override void Activate(Transform player)
    {
        if (manager == null || manager.specialPaintingTarget == null)
        {
            completed = true;
            return;
        }

        // Setup state
        state = new State();
        state.painting = manager.specialPaintingTarget;
        state.originalPosition = state.painting.position;
        state.originalRotation = state.painting.rotation;

        // compute fallen position / rotation with small defaults; allow PaintingFallConfig to override if present
        var cfg = state.painting.GetComponent<PaintingFallConfig>();
        Vector3 offsetWorld;
        if (cfg != null)
            offsetWorld = state.painting.TransformVector(cfg.fallOffset);
        else
            offsetWorld = state.painting.forward * DefaultFallForward + Vector3.down * DefaultFallDepth;

        state.fallenPosition = state.originalPosition + offsetWorld;
        if (cfg != null)
            state.fallenRotation = state.originalRotation * Quaternion.Euler(cfg.fallRotationEuler);
        else
            state.fallenRotation = state.originalRotation * Quaternion.Euler(DefaultFallRotationX, 0f, 0f);

        state.fallDuration = (cfg != null && cfg.fallDuration > 0f) ? cfg.fallDuration : DefaultFallDuration;
        state.returnDuration = (cfg != null && cfg.returnDuration > 0f) ? cfg.returnDuration : DefaultReturnDuration;

        state.fallTimer = 0f;
        state.returnTimer = 0f;
        state.isFalling = false;
        state.hasFallen = false;
        state.isReturning = false;
        state.completed = false;

        phoneTimer = 0f;
        phoneAllowed = false;
    }

    public override void Tick(Transform player)
    {
        if (completed || state == null || state.painting == null) return;

        float dt = Time.deltaTime;

        // Only check the explicitly assigned CamImage on TaskManager.
        // If no CamImage assigned, do not trigger the fall — this makes the check deterministic and easy to test.
        CamImage targetCamImage = manager != null ? manager.specialCamImage : null;
        if (targetCamImage == null)
        {
            // do not progress to the zoom check without an assigned CamImage
            // still allow phone gating to advance if desired (keeps internal timer behavior consistent)
            if (!phoneAllowed)
            {
                bool advance = true;
                if (manager != null && manager.specialPhoneUseCall)
                    advance = manager.specialPhoneCallStarted;

                if (advance)
                {
                    phoneTimer += dt;
                    if (phoneTimer >= phoneDelay)
                        phoneAllowed = true;
                }
            }
            return;
        }

        // phone gating:
        // - if manager.specialPhoneUseCall == true, phone delay only advances once manager.specialPhoneCallStarted == true
        // - otherwise phone delay starts immediately
        if (!phoneAllowed)
        {
            bool advance = true;
            if (manager != null && manager.specialPhoneUseCall)
            {
                advance = manager.specialPhoneCallStarted;
            }

            if (advance)
            {
                phoneTimer += dt;
                if (phoneTimer >= phoneDelay)
                {
                    phoneAllowed = true;
                }
            }
        }

        // trigger fall when phone allowed AND selected camera is zoomed
        if (!state.isFalling && !state.hasFallen)
        {
            if (phoneAllowed && targetCamImage.IsZoomed())
            {
                state.isFalling = true;
                state.fallTimer = 0f;
            }
            return;
        }

        // falling animation
        if (state.isFalling)
        {
            state.fallTimer += dt;
            float f = Mathf.Clamp01(state.fallTimer / Mathf.Max(0.0001f, state.fallDuration));
            state.painting.position = Vector3.Lerp(state.originalPosition, state.fallenPosition, f);
            state.painting.rotation = Quaternion.Slerp(state.originalRotation, state.fallenRotation, f);
            if (f >= 1f)
            {
                state.isFalling = false;
                state.hasFallen = true;
            }
            return;
        }

        // player returns painting
        if (state.hasFallen && !state.isReturning)
        {
            if (player == null) return;
            float d = Vector3.Distance(player.position, state.painting.position);
            if (d <= manager.paintingInteractionDistance && manager.InteractPressed())
            {
                state.isReturning = true;
                state.returnTimer = 0f;
            }
            return;
        }

        // returning animation
        if (state.isReturning)
        {
            state.returnTimer += dt;
            float f = Mathf.Clamp01(state.returnTimer / Mathf.Max(0.0001f, state.returnDuration));
            state.painting.position = Vector3.Lerp(state.fallenPosition, state.originalPosition, f);
            state.painting.rotation = Quaternion.Slerp(state.painting.rotation, state.originalRotation, f);
            if (f >= 1f)
            {
                state.isReturning = false;
                state.hasFallen = false;
                state.completed = true;
                completed = true;

                // play completion clip via TaskManager's specialPhoneAudioSource if available
                if (manager != null && manager.specialPhoneAudioSource != null && manager.specialPhoneCompleteClip != null)
                {
                    manager.specialPhoneAudioSource.PlayOneShot(manager.specialPhoneCompleteClip);
                }
            }
            return;
        }
    }

    public override void Deactivate()
    {
        // leave painting where it is; clear state
        state = null;
        completed = false;
        phoneTimer = 0f;
        phoneAllowed = false;
    }

    public override bool IsCompleted => completed;
}
