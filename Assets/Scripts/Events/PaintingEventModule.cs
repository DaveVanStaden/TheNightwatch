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
        public float applyAccum;            // accumulation time while waiting to apply distortion
        public float protectedTimeRemaining; // time remaining where painting must stay distorted (positive)
        public float resetAttemptAccum;     // accumulation time while attempting to reset after protection

        public PaintingState(Painting p)
        {
            this.p = p;
            applyAccum = 0f;
            protectedTimeRemaining = 0f;
            resetAttemptAccum = 0f;
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

                        if (Random.value < roll)
                        {
                            float duration = Random.Range(manager.resetMinSeconds, manager.resetMaxSeconds);
                            if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] Distorting '{p.gameObject.name}' for {duration:F1}s");
                            p.ApplyDistortion();
                            st.protectedTimeRemaining = duration; // positive protected time remaining
                            st.applyAccum = 0f;
                            st.resetAttemptAccum = 0f;
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
            else
            {
                // Painting is distorted.
                if (st.protectedTimeRemaining > 0f)
                {
                    // still in forced duration
                    st.protectedTimeRemaining = Mathf.Max(0f, st.protectedTimeRemaining - dt);
                    if (manager.debugPaintings)
                        Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' protected for {st.protectedTimeRemaining:F1}s more");
                    continue; // cannot reset yet
                }

                // now allowed to attempt reset (only when not being looked at)
                bool looking = p.IsPlayerLooking(cam, manager.paintingLookAngle);
                if (!looking)
                {
                    st.resetAttemptAccum += dt;

                    float sanityFactor = Mathf.Clamp01(sanity / 100f); // assume sanity 0-100
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
                    }
                }
                else
                {
                    // player is looking -> pause reset attempts (do not accumulate)
                    if (manager.debugPaintings) Debug.Log($"[PaintingEventModule] '{p.gameObject.name}' is being watched - pausing reset attempts.");
                }
            }
        }
    }
}
