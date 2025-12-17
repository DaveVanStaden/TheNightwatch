using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Event module that occasionally "crashes" a single BreakerButton:
/// - Chooses a random BreakerButton that is currently ON (isOn == true) and active in scene
/// - Calls Toggle() once on that button (one-time event)
/// - If no buttons are ON, the event does not run
/// - Starts a cooldown after firing; chance is computed per-frame from manager.baseChancePerSecond (Poisson)
/// - Uses a sanity threshold and a module-level happened flag similar to CameraEventModule
/// </summary>
public class BreakerEventModule : IEventModule
{
    private float cooldownTimer = 0f;
    private bool happenedSinceBelowThreshold = false;

    // fallback cooldown if EventManager's values are invalid
    private const float DefaultBreakerEventCooldown = 120f;

    public BreakerEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        // pick an initial randomized cooldown between configured min/max (safe fallbacks)
        float minCd = (manager != null) ? manager.breakerEventMinCooldownSeconds : 0f;
        float maxCd = (manager != null) ? manager.breakerEventMaxCooldownSeconds : 0f;

        if (minCd > 0f && maxCd >= minCd)
            cooldownTimer = Random.Range(minCd, maxCd);
        else if (maxCd > 0f)
            cooldownTimer = maxCd;
        else
            cooldownTimer = DefaultBreakerEventCooldown;

        happenedSinceBelowThreshold = false;
    }

    public override void OnUpdate()
    {
        if (manager == null) return;
        float dt = Time.deltaTime;

        // decrement cooldown and reset happened flag on expiry
        float oldCd = cooldownTimer;
        if (cooldownTimer > 0f)
            cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);

        if (oldCd > 0f && cooldownTimer <= 0f)
            happenedSinceBelowThreshold = false;

        // Reset the "happened" flag when sanity goes above threshold
        if (manager.playerSanity >= manager.breakerEventSanityThreshold)
            happenedSinceBelowThreshold = false;

        // If we've already happened while below threshold, don't fire again until cooldown/reset
        if (happenedSinceBelowThreshold) return;

        // Only attempt event when sanity is below threshold
        if (manager.playerSanity >= manager.breakerEventSanityThreshold) return;

        // Only attempt spawn if cooldown expired
        if (cooldownTimer > 0f) return;

        // Gather eligible buttons (must be active in hierarchy and currently ON)
        var allButtons = UnityEngine.Object.FindObjectsByType<BreakerButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var eligible = new List<BreakerButton>();
        if (allButtons != null)
        {
            foreach (var b in allButtons)
            {
                if (b == null) continue;
                if (!b.gameObject.activeInHierarchy) continue;
                if (b.isOn) eligible.Add(b);
            }
        }

        // If no buttons are ON, do not attempt the event
        if (eligible.Count == 0) return;

        // compute per-frame probability using Poisson: p = 1 - exp(-rate * dt)
        float chancePerSecond = manager.baseChancePerSecond;
        float rollThreshold = 1f - Mathf.Exp(-chancePerSecond * dt);

        if (UnityEngine.Random.value < rollThreshold)
        {
            TryCrashRandomButton(eligible);
        }
    }

    private void TryCrashRandomButton(List<BreakerButton> eligible)
    {
        if (eligible == null || eligible.Count == 0) return;

        var chosen = eligible[UnityEngine.Random.Range(0, eligible.Count)];
        if (chosen == null) return;

        // Ensure it is still ON (safety)
        if (!chosen.isOn) return;

        try
        {
            chosen.Toggle();
        }
        catch
        {
            // ignore errors from Toggle invocation
        }

        // start randomized cooldown immediately between configured min/max (safe fallback)
        float minCd = (manager != null) ? manager.breakerEventMinCooldownSeconds : 0f;
        float maxCd = (manager != null) ? manager.breakerEventMaxCooldownSeconds : 0f;

        if (minCd > 0f && maxCd >= minCd)
            cooldownTimer = Random.Range(minCd, maxCd);
        else if (maxCd > 0f)
            cooldownTimer = maxCd;
        else
            cooldownTimer = DefaultBreakerEventCooldown;

        // mark that event happened while below threshold so it doesn't immediately retrigger
        happenedSinceBelowThreshold = true;
    }
}