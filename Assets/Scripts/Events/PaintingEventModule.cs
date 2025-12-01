using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles timed/randomized painting distortion events.
/// Replaced Dictionary-based tracking with an explicit per-painting state list to avoid
/// subtle reference/lookup issues and make the cooldown logic explicit and easier to follow.
/// </summary>
public class PaintingEventModule : IEventModule
{
    private class PaintingState
    {
        public Painting p;
        public float applyAccum;             // accumulation time while waiting to apply distortion
        public float protectedTimeRemaining; // time remaining where painting must stay distorted (positive)
        public float resetAttemptAccum;      // accumulation time while attempting to reset after protection

        // New runtime flags:
        public bool seenWhileDistorted;      // set when player has looked at the painting while it was distorted
        public bool wasLookingLastFrame;     // previous-frame looking state used to detect "look away" transitions

        public PaintingState(Painting p)
        {
            this.p = p;
            applyAccum = 0f;
            protectedTimeRemaining = 0f;
            resetAttemptAccum = 0f;
            seenWhileDistorted = false;
            wasLookingLastFrame = false;
        }
    }

    private readonly List<PaintingState> states = new List<PaintingState>();

    public PaintingEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        BuildStatesFromManager();
    }

    public override void OnStart()
    {
        // ensure states are correct at start
        BuildStatesFromManager();
    }

    private void BuildStatesFromManager()
    {
        states.Clear();
        if (manager.paintings == null) return;
        foreach (var p in manager.paintings)
        {
            if (p != null) states.Add(new PaintingState(p));
        }
    }

    public override void OnUpdate()
    {
        if (manager.paintings == null || manager.paintings.Count == 0) return;

        // If the painting list changed at runtime, resync the states
        if (states.Count != manager.paintings.Count)
        {
            // cheap resync: rebuild fully (keeps code simple and robust)
            BuildStatesFromManager();
        }

        Camera cam = manager.GetPlayerCamera();
        if (cam == null)
        {
            if (manager.debugPaintings) Debug.LogWarning("[PaintingEventModule] Player camera is null.");
            return;
        }

        float sanity = manager.playerSanity;
        float dt = Time.deltaTime;

        for (int i = 0; i < states.Count; i++)
        {
            var st = states[i];
            var p = st.p;
            if (p == null) continue;

            // If not distorted, try to trigger distortion when player not looking and sanity low
            if (!p.IsDistorted)
            {
                // clear any reset attempt (shouldn't be relevant while not distorted)
                st.resetAttemptAccum = 0f;

                if (sanity < manager.sanityThreshold)
                {
                    bool looking = p.IsPlayerLooking(cam, manager.paintingLookAngle);
                    if (!looking)
                    {
                        st.applyAccum += dt;

                        float sanityFactor = Mathf.Clamp01((manager.sanityThreshold - sanity) / Mathf.Max(0.0001f, manager.sanityThreshold));
                        float timeRatio = Mathf.Clamp01(st.applyAccum / Mathf.Max(0.0001f, manager.timeToMaxChance));
                        float effectiveChancePerSecond = manager.baseChancePerSecond * (1f + sanityFactor) * (0.01f + timeRatio);
                        float roll = effectiveChancePerSecond * dt;

                        if (manager.debugPaintings)
                        {
                            Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' applyAccum={st.applyAccum:F2} timeRatio={timeRatio:F2} sanityFactor={sanityFactor:F2} effChance/s={effectiveChancePerSecond:F4} roll={roll:F6}");
                        }

                        // Replace the block that handles a successful ApplyDistortion roll so we also reset the new flags
                        if (Random.value < roll)
                        {
                            float duration = Random.Range(manager.resetMinSeconds, manager.resetMaxSeconds);
                            if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] Distorting '{p.gameObject.name}' for {duration:F1}s");
                            p.ApplyDistortion();

                            // set protected time and reset accumulators / flags
                            st.protectedTimeRemaining = duration; // positive protected time remaining
                            st.applyAccum = 0f;
                            st.resetAttemptAccum = 0f;

                            // New: reset the "seen while distorted" and looking-tracking flags so we can detect a subsequent look+look-away
                            st.seenWhileDistorted = false;
                            st.wasLookingLastFrame = false;
                        }
                    }
                    else
                    {
                        // player is looking -> reset apply accumulation
                        if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' is being looked at - resetting applyAccum.");
                        st.applyAccum = 0f;
                    }
                }
                else
                {
                    // sanity not low enough -> slowly decay accumulation
                    st.applyAccum = Mathf.Max(0f, st.applyAccum - dt * 0.5f);
                }
            }
            // Replace the "Painting is distorted." branch with this updated logic (handles auto-revert at sanity>=75)
            else
            {
                // Painting is distorted.
                if (st.protectedTimeRemaining > 0f)
                {
                    // still in forced duration
                    st.protectedTimeRemaining = Mathf.Max(0f, st.protectedTimeRemaining - dt);
                    if (manager.debugPaintings)
                        Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' protected for {st.protectedTimeRemaining:F1}s more");

                    // update looking flag while protected so we don't lose sight-tracking
                    bool lookingNowDuringProtected = p.IsPlayerLooking(cam, manager.paintingLookAngle);
                    st.wasLookingLastFrame = lookingNowDuringProtected;
                    if (lookingNowDuringProtected)
                        st.seenWhileDistorted = true;

                    continue; // cannot reset yet
                }

                // Replace the sanity check that triggers immediate revert with a dual-scale check (handles 0-1 or 0-100 sanity)
                if (manager.playerSanity >= 75f)
                {
                    if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] Sanity >=75 — reverting '{p.gameObject.name}' immediately.");
                    p.RevertToOriginal();
                    st.applyAccum = 0f;
                    st.resetAttemptAccum = 0f;
                    st.protectedTimeRemaining = 0f;
                    st.seenWhileDistorted = false;
                    st.wasLookingLastFrame = false;
                    continue;
                }

                // now allowed to attempt reset (only when not being looked at)
                bool lookingNow = p.IsPlayerLooking(cam, manager.paintingLookAngle);

                // Record when player looks while distorted so we can trigger a reset attempt on the next look-away
                if (lookingNow)
                {
                    st.seenWhileDistorted = true;
                    // pause reset attempts while player is looking
                    if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' is being watched - pausing reset attempts.");
                    st.wasLookingLastFrame = true;
                    continue;
                }

                // If player just looked away (was looking last frame), and they've seen it while distorted,
                // seed an immediate attempt by bumping resetAttemptAccum to max.
                if (st.wasLookingLastFrame && st.seenWhileDistorted)
                {
                    st.resetAttemptAccum = Mathf.Max(st.resetAttemptAccum, manager.timeToMaxChance);
                }

                // accumulate reset attempt time while not looking
                st.resetAttemptAccum += dt;

                // Higher sanity reduces reset chance (so resets build up slower at high sanity)
                float sanityFactor = 1f - Mathf.Clamp01(manager.playerSanity / 100f);

                float timeRatio = Mathf.Clamp01(st.resetAttemptAccum / Mathf.Max(0.0001f, manager.timeToMaxChance));
                float effectiveChancePerSecond = manager.baseChancePerSecond * (1f + sanityFactor) * (0.01f + timeRatio);
                float roll = effectiveChancePerSecond * dt;

                if (manager.debugPaintings)
                {
                    Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' resetAccum={st.resetAttemptAccum:F2} timeRatio={timeRatio:F2} sanityFactor={sanityFactor:F2} effChance/s={effectiveChancePerSecond:F4} roll={roll:F6}");
                }

                if (Random.value < roll)
                {
                    if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] Reverting '{p.gameObject.name}' to original.");
                    p.RevertToOriginal();
                    st.applyAccum = 0f;
                    st.resetAttemptAccum = 0f;
                    st.protectedTimeRemaining = 0f;
                    st.seenWhileDistorted = false;
                    st.wasLookingLastFrame = false;
                }
                else
                {
                    // update last-looking flag for next frame
                    st.wasLookingLastFrame = false;
                }
            }
        }
    }
}
