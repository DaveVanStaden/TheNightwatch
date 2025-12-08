using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ShadowLogic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private float minMoveDistance = 30f;
    // [SerializeField] private float minPlayerDistance = 20f; // legacy - replaced by below
    [SerializeField, Tooltip("Distance at which the AI will trigger a reposition (player proximity trigger)")]
    private float repositionTriggerDistance = 20f;
    [SerializeField, Tooltip("Distance the AI will try to keep from the player when choosing a peek position")]
    private float maintainDistanceFromPlayer = 20f;
    [SerializeField] private float lightDisableRadius = 10f;
    [SerializeField] private int sampleCount = 32;
    [SerializeField] private float searchRadius = 20f;
    [SerializeField] private float killRange = 10f; // Set as needed
    [SerializeField] private float sanityDrainPerSecond = 5f; // Use float, not int
    [SerializeField] private float KillSanityThresshold = 60f;

    // Add these fields (place with other private fields)
    [SerializeField, Tooltip("Sanity at which shadow is still mostly faint (upper bound)")]
    private float alphaHighSanityThreshold = 80f;
    [SerializeField, Tooltip("Sanity at which shadow reaches max opacity (lower bound)")]
    private float alphaLowSanityThreshold = 20f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Alpha when player sanity is high (faint)")]
    private float minShadowAlpha = 0.10f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Alpha when player sanity is low (visible)")]
    private float maxShadowAlpha = 1.0f;

    [Header("Movement Settings")]
    [Tooltip("Agent speed when player sanity is high (slow)")]
    [SerializeField] private float minMoveSpeed = 1.5f;
    [Tooltip("Agent speed when player sanity is low (fast)")]
    [SerializeField] private float maxMoveSpeed = 6f;
    [Tooltip("Sanity at which movement is still slow (upper bound)")]
    [SerializeField] private float speedHighSanityThreshold = 80f;
    [Tooltip("Sanity at which movement reaches max speed (lower bound)")]
    [SerializeField] private float speedLowSanityThreshold = 10f;

    // Hunt-specific speed (scales from KillSanityThresshold -> 0)
    [Tooltip("Agent speed when hunting - minimum")]
    [SerializeField] private float huntMinSpeed = 3f;
    [Tooltip("Agent speed when hunting - maximum at 0 sanity")]
    [SerializeField] private float huntMaxSpeed = 10f;

    private Vector3 peekDestination;
    private bool isVisible = false;
    private HashSet<Light> disabledLights = new HashSet<Light>();

    private float outOfSightTimer = 0f;
    [SerializeField]private float outOfSightThreshold = 15f;

    private bool repositioningDueToproximity = false;
    private bool wasVisibleLastFrame = true;

    private PlayerStats playerStats;
    private PlayerManager playerManager;
    private PlayerCameraLook playerCameraLook;
    private bool isHunting = false;

    private bool isAttacking = false;
    private float attackWindupTimer = 0f;
    private const float attackWindupDuration = 0.5f;

    // Reposition / lifetime counters
    private int repositionsDone = 0;
    [SerializeField, Tooltip("Maximum number of reposition attempts before this shadow despawns.")]
    private int repositionsBeforeDestroy = 4;
    private bool destroyScheduled = false;
    private bool initialPeekDone = false;
    private bool repositionPending = false;

    [Header("Reposition / Hunt")]
    [SerializeField, Tooltip("Seconds to wait before performing a reposition after being triggered (shadow disappears during this time).")]
    private float repositionCooldownSeconds = 15f;
    private bool repositionScheduled = false;
    private float repositionTimer = 0f;
    private bool repositionLocked = false;

    [SerializeField, Tooltip("Maximum time (seconds) the shadow will hunt the player before despawning.")]
    private float huntMaxDurationSeconds = 30f;
    private float huntTimer = 0f;

    // Add these fields with other private fields (reposition/lifetime counters)
    [SerializeField, Tooltip("Seconds to suppress re-entering hunt after an attack/miss")]
    private float huntSuppressDuration = 1.0f;
    private float huntSuppressTimer = 0f;

    // Flicker management
    private readonly Dictionary<Light, Coroutine> flickerCoroutines = new Dictionary<Light, Coroutine>();
    private readonly Dictionary<Light, float> originalIntensities = new Dictionary<Light, float>();

    [Header("Light Darken Settings")]
    [Tooltip("How dark the light becomes (0 = off, 1 = no change)")]
    [SerializeField, Range(0f, 1f)] private float lightDarkenFactor = 0.25f;
    [Tooltip("How long (seconds) the light fades to the darkened value")]
    [SerializeField] private float lightFadeDuration = 0.05f;

    private Renderer[] cachedRenderers;
    private Material[] cachedMaterials;

    private void Awake()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        peekDestination = transform.position;

        // initial find, do not count this first placement
        initialPeekDone = false;
        FindPeekPosition();
        initialPeekDone = true;

        playerStats = Object.FindFirstObjectByType<PlayerStats>();

        // Get PlayerManager from playerTransform; do NOT try to FindObjectOfType<PlayerCameraLook>()
        playerManager = playerTransform != null ? playerTransform.GetComponent<PlayerManager>() : null;
        if (playerManager == null)
        {
            // try a scene-wide find of the PlayerManager (Unity object)
            playerManager = FindObjectOfType<PlayerManager>();
        }
        playerCameraLook = playerManager != null ? playerManager.cameraLookModule : null;

        // Cache renderers and create instance materials to control alpha safely
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        if (cachedRenderers != null && cachedRenderers.Length > 0)
        {
            cachedMaterials = new Material[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                    cachedMaterials[i] = cachedRenderers[i].material; // creates instance
            }
        }

        // Validate darken settings
        lightDarkenFactor = Mathf.Clamp01(lightDarkenFactor);
        lightFadeDuration = Mathf.Max(0f, lightFadeDuration);
    }

    private void Update()
    {
        // keep essential null checks (don't block on playerCameraLook)
        if (playerTransform == null || agent == null || playerStats == null)
            return;

        // try to resolve playerManager/playerCameraLook at runtime if missing (no Unity API for PlayerCameraLook)
        if (playerManager == null && playerTransform != null)
            playerManager = playerTransform.GetComponent<PlayerManager>();
        if (playerManager == null)
            playerManager = FindObjectOfType<PlayerManager>();
        if (playerCameraLook == null && playerManager != null)
            playerCameraLook = playerManager.cameraLookModule;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Update shadow opacity based on current sanity every frame
        UpdateShadowAlpha();
        // Process scheduled reposition timer (shadow disappears immediately when scheduled,
        // then after timer expires it will choose a new peek position and move)
        if (repositionScheduled)
        {
            repositionTimer -= Time.deltaTime;
            if (repositionTimer <= 0f)
            {
                repositionScheduled = false;
                repositionLocked = false; // allow movement to start once the cooldown expired
                FindPeekPosition();
            }
        }

        // If currently hunting, track hunt duration and despawn if exceeded
        if (isHunting)
        {
            huntTimer += Time.deltaTime;
            if (huntTimer >= huntMaxDurationSeconds)
            {
                Debug.Log("[ShadowLogic] Hunt max duration exceeded — despawning shadow.");
                Destroy(gameObject);
                return;
            }
        }

        // --- Movement speed scaling by sanity ---
        if (agent != null && playerStats != null)
        {
            // Use the same hunting condition as the hunt branch so speed is correct even before isHunting is toggled
            float sanityF = (float)playerStats.Sanity;
            bool huntingThisFrame = playerStats.Sanity < KillSanityThresshold && (playerStats.Sanity <= 0 || huntSuppressTimer <= 0f);

            if (huntingThisFrame)
            {
                // Scale hunt speed from KillSanityThresshold -> 0 (0 sanity = max hunt speed)
                float t = Mathf.InverseLerp(KillSanityThresshold, 0f, sanityF);
                t = Mathf.Clamp01(t);
                agent.speed = Mathf.Lerp(huntMinSpeed, huntMaxSpeed, t);
            }
            else
            {
                // Reposition / normal movement scaling (unchanged)
                float t = Mathf.InverseLerp(speedHighSanityThreshold, speedLowSanityThreshold, sanityF);
                t = Mathf.Clamp01(t);
                agent.speed = Mathf.Lerp(minMoveSpeed, maxMoveSpeed, t);
            }
        }

        // Sanity drain if player is looking at hallucination AND the hallucination is visible to the player
        // but only if the shadow is NOT currently moving
        // Replace the existing sanity-drain block with this guarded check
        if (!repositionScheduled && !repositionLocked && IsPlayerLookingAtMe() && !IsMoving() && IsPlayerLineOfSightClear())
        {
            playerStats.ChangeSanity(-sanityDrainPerSecond * Time.deltaTime);
            Debug.Log("[ShadowLogic] Draining sanity! " + playerStats.Sanity);
        }

        // HUNT LOGIC: If sanity < threshold, only hunt/attack, no repositioning or avoidance
        // allow relentless chase only when sanity <= 0, otherwise respect suppress timer
        if (playerStats.Sanity < KillSanityThresshold && (playerStats.Sanity <= 0 || huntSuppressTimer <= 0f))
        {
            if (!isHunting)
            {
                isHunting = true;
                huntTimer = 0f; // start tracking hunt duration
                Debug.Log("[ShadowLogic] HUNT MODE: Player is being hunted!");
            }

            HuntPlayer(); // Move toward attack position

            // Attack logic
            if (distanceToPlayer <= killRange)
            {
                // stop agent movement while winding up so it doesn't ram the player
                if (agent != null)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }

                if (!isAttacking)
                {
                    isAttacking = true;
                    attackWindupTimer = 0f;
                    Debug.Log("[ShadowLogic] Attack windup started!");
                }
                else
                {
                    attackWindupTimer += Time.deltaTime;
                    if (attackWindupTimer >= attackWindupDuration)
                    {
                        float currentDistance = Vector3.Distance(transform.position, playerTransform.position);
                        if (currentDistance <= killRange)
                        {
                            Debug.Log("player got hit and dies");
                            // TODO: call player death / damage logic here if needed

                            // After a successful hit:
                            // - If player's sanity is <= 0, continue attacking (don't reposition).
                            // - Otherwise, stop hunting and pick a new peek position before attacking again.
                            isAttacking = false;
                            attackWindupTimer = 0f;

                            if (playerStats != null && playerStats.Sanity <= 0)
                            {
                                // keep attacking
                                isHunting = true;
                                // keep agent stopped so repeated attacks can happen
                                if (agent != null) agent.isStopped = true;
                            }
                            else
                            {
                                // reposition before attacking again
                                isHunting = false;
                                if (agent != null) agent.isStopped = false;
                                FindPeekPosition();
                                // After calling FindPeekPosition(), set the suppress timer
                                huntSuppressTimer = huntSuppressDuration;
                            }
                        }
                        else
                        {
                            Debug.Log("[ShadowLogic] Attack missed, player moved away! Repositioning before next attack.");
                            // After a miss, stop hunting and force a reposition before attempting to attack again
                            isAttacking = false;
                            isHunting = false;
                            attackWindupTimer = 0f;
                            if (agent != null) agent.isStopped = false;
                            FindPeekPosition();
                            // After calling FindPeekPosition(), set the suppress timer
                            huntSuppressTimer = huntSuppressDuration;
                        }
                    }
                }
            }
            else
            {
                // Player moved out of attack range, cancel attack and resume movement
                if (isAttacking)
                {
                    Debug.Log("[ShadowLogic] Attack cancelled, player moved out of range.");
                    isAttacking = false;
                    attackWindupTimer = 0f;
                }
                if (agent != null)
                    agent.isStopped = false;
            }
            EnableRenderer(); // Ensure visible while hunting
            return; // Prevent repositioning/avoidance while hunting
        }

        // Not hunting, reset attack state
        if (isHunting)
        {
            isHunting = false;
            isAttacking = false;
            attackWindupTimer = 0f;
            if (agent != null)
                agent.isStopped = false;
            Debug.Log("[ShadowLogic] Repositioning: Player sanity recovered.");
        }
        StandardAIUpdate(); // Only runs when not hunting

        if (huntSuppressTimer > 0f)
            huntSuppressTimer -= Time.deltaTime;


    }

    /// <summary>
    /// Rewritten FindPeekPosition:
    /// - Samples positions around the PLAYER (not around the shadow) to ensure the hallucination moves to stalk the player.
    /// - Prefers positions on the NavMesh that can see the player and are approximately at maintainDistanceFromPlayer.
    /// - Avoids using the old candidate = transform.position + dir * Random.Range(minMoveDistance, searchRadius) which could send it far away.
    /// </summary>
    private void FindPeekPosition()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[ShadowLogic] FindPeekPosition: playerTransform is null.");
            return;
        }

        Vector3 bestCandidate = transform.position;
        float bestScore = float.MaxValue;
        bool foundAny = false;

        // Desired ring radius from player
        float desiredRadius = Mathf.Max(0.1f, maintainDistanceFromPlayer);
        float jitterRadius = Mathf.Max(0f, searchRadius);

        for (int i = 0; i < sampleCount; i++)
        {
            // sample around the player (not around the shadow)
            Vector2 unit = Random.insideUnitCircle.normalized;
            // place mostly on the ring at desiredRadius with some jitter
            float r = desiredRadius + Random.Range(-jitterRadius * 0.5f, jitterRadius * 0.5f);
            r = Mathf.Max(1f, r);
            Vector3 candidate = playerTransform.position + new Vector3(unit.x * r, 0f, unit.y * r);

            // sample navmesh near candidate
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 4.0f, NavMesh.AllAreas))
                continue;

            // reject positions that are too close to the player (avoid overlap) or inside obstacles
            float playerDist = Vector3.Distance(hit.position, playerTransform.position);
            if (playerDist < 1.0f) continue;

            // Prefer positions that have line-of-sight to the player (stalking / peeking)
            bool canSee = CanSeePlayerFromPosition(hit.position);

            // Score: prefer positions that are reachable, canSee==true, and close to desiredRadius.
            float score = Mathf.Abs(playerDist - desiredRadius);
            if (!canSee) score += 1000f; // penalize positions that cannot see the player

            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = hit.position;
                foundAny = true;
            }
        }

        if (foundAny)
        {
            SetPeekDestination(bestCandidate, "FindPeekPosition (around player)");
            Debug.Log("[ShadowLogic] Moving to peek position near player at " + bestCandidate);
        }
        else
        {
            // Fallback: sample directly near the player (larger radius) to avoid going to the other side of the map.
            NavMeshHit fallbackHit;
            Vector3 fallbackTry = playerTransform.position + (playerTransform.forward * -desiredRadius); // behind player
            if (NavMesh.SamplePosition(fallbackTry, out fallbackHit, Mathf.Max(4f, jitterRadius + 2f), NavMesh.AllAreas))
            {
                SetPeekDestination(fallbackHit.position, "FindPeekPosition fallback behind player");
                Debug.LogWarning("[ShadowLogic] No ideal peek found; using fallback behind player.");
            }
            else
            {
                // last resort: stay close to current position
                SetPeekDestination(transform.position, "FindPeekPosition none");
                Debug.LogWarning("[ShadowLogic] No valid stalk position found near player, staying put.");
            }
        }
    }

    // Centralized setter so we can count reposition attempts and schedule destruction
    // Modified SetPeekDestination: stop counting here, mark pending instead
    private void SetPeekDestination(Vector3 newDestination, string debugContext = null)
    {
        if (Vector3.Distance(peekDestination, newDestination) > 0.1f)
        {
            // Immediately restore lights when a reposition is chosen
            RestoreLightsImmediately();

            peekDestination = newDestination;

            // mark that a reposition has been requested — count it when movement actually starts
            repositionPending = initialPeekDone; // don't count the initial placement
        }
    }

    //checks if player is visible from a given position
    private bool CanSeePlayerFromPosition(Vector3 fromPosition)
    {
        if (playerTransform == null) return false;

        Vector3[] playerPoints = new Vector3[]
        {
            playerTransform.position + Vector3.up * 1.6f,
            playerTransform.position + Vector3.up * 0.9f,
            playerTransform.position + Vector3.up * 0.2f
        };

        int mask = LayerMask.GetMask("Walls", "Obstacles") | LayerMask.GetMask("Player"); // Adjust as needed

        foreach (var point in playerPoints)
        {
            Vector3 origin = fromPosition + Vector3.up * 1.0f;
            Vector3 dir = (point - origin).normalized;
            float dist = Vector3.Distance(origin, point) + 1.0f; // Add buffer

            Debug.DrawLine(origin, point, Color.red, 0.5f);

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, mask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Player"))
                    return true;
                else if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Player"))
                    break;
            }
        }
        return false;
    }

    // Flicker implementation: manage coroutines per-light
    private void ManageNearbyLights()
    {
        var lights = FindObjectsOfType<Light>();
        var inRange = new HashSet<Light>();

        foreach (var light in lights)
        {
            if (light == null) continue;
            float dist = Vector3.Distance(transform.position, light.transform.position);
            if (dist <= lightDisableRadius)
            {
                inRange.Add(light);
                // record original intensity and start darken coroutine if not already
                if (!originalIntensities.ContainsKey(light))
                    originalIntensities[light] = light.intensity;

                if (!flickerCoroutines.ContainsKey(light))
                {
                    var co = StartCoroutine(FlickerLight(light));
                    flickerCoroutines.Add(light, co);
                }
            }
        }

        // stop darken coroutines for lights that left range, ensure restore
        var toStop = new List<Light>();
        foreach (var kv in flickerCoroutines)
        {
            var light = kv.Key;
            if (!inRange.Contains(light))
                toStop.Add(light);
        }
        foreach (var light in toStop)
        {
            StopFlicker(light);
        }
    }

    // Replace FlickerLight with this coroutine that fades intensity instead of toggling enabled
    private IEnumerator FlickerLight(Light light)
    {
        if (light == null) yield break;
        if (!originalIntensities.TryGetValue(light, out var original))
            original = light.intensity;

        float dark = original * Mathf.Clamp01(lightDarkenFactor);

        // Fade from current intensity to dark target
        yield return StartCoroutine(FadeLightIntensity(light, light.intensity, dark, lightFadeDuration));

        // Hold the darkened state until stopped
        while (true)
        {
            if (light == null) break;
            yield return null;
        }
    }

    // Helper: smooth fade
    private IEnumerator FadeLightIntensity(Light light, float from, float to, float duration)
    {
        if (light == null)
            yield break;
        if (duration <= 0f)
        {
            light.intensity = to;
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            light.intensity = Mathf.Lerp(from, to, t);
            yield return null;
        }
        light.intensity = to;
    }

    // Replace StopFlicker with this (restores intensity and clears dictionaries)
    private void StopFlicker(Light light)
    {
        if (light == null) return;
        if (flickerCoroutines.TryGetValue(light, out var co))
        {
            if (co != null)
                StopCoroutine(co);
            flickerCoroutines.Remove(light);
        }

        // restore original intensity if known
        if (originalIntensities.TryGetValue(light, out var orig))
        {
            light.intensity = orig;
            originalIntensities.Remove(light);
        }
        else
        {
            light.enabled = true; // fallback
        }
    }

    // Replace RestoreLightsImmediately with this (stop all coroutines, restore intensities)
    private void RestoreLightsImmediately()
    {
        if (flickerCoroutines.Count == 0 && originalIntensities.Count == 0)
            return;

        foreach (var kv in new List<KeyValuePair<Light, Coroutine>>(flickerCoroutines))
        {
            var light = kv.Key;
            if (light == null) continue;
            if (kv.Value != null)
                StopCoroutine(kv.Value);
            flickerCoroutines.Remove(light);
        }

        foreach (var kv in new List<KeyValuePair<Light, float>>(originalIntensities))
        {
            var light = kv.Key;
            var orig = kv.Value;
            if (light == null) continue;
            light.intensity = orig;
            originalIntensities.Remove(light);
        }
    }

    private void EnableRenderer()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;
    }

    private void DisableRenderer()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = false;
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector3[] playerPoints = new Vector3[]
        {
        playerTransform.position + Vector3.up * 1.6f, // head
        playerTransform.position + Vector3.up * 0.9f, // torso
        playerTransform.position + Vector3.up * 0.2f  // feet
        };

        int mask = LayerMask.GetMask("Walls", "Obstacles") | LayerMask.GetMask("Player"); // Use same mask

        foreach (var point in playerPoints)
        {
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 dir = (point - origin).normalized;
            float dist = Vector3.Distance(origin, point) + 1.0f; // Add buffer

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, mask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Player"))
                    return true;
                else if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Player"))
                    break;
            }
        }
        return false; // All points are blocked
    }

    private bool IsPlayerLookingAtMe()
    {
        // playerCameraLook is NOT a UnityEngine.Object — use it if available, otherwise fall back to a camera transform
        Vector3 cameraForward;
        Vector3 origin;

        if (playerManager != null && playerManager.playerCamera != null)
        {
            origin = playerManager.playerCamera.transform.position;
            cameraForward = playerManager.playerCamera.transform.forward;
        }
        else if (playerCameraLook != null)
        {
            // Use the module's camera forward (no Unity cast)
            origin = playerTransform != null ? playerTransform.position + Vector3.up * 1.6f : Vector3.zero;
            cameraForward = playerCameraLook.GetCameraForward();
        }
        else if (Camera.main != null)
        {
            origin = Camera.main.transform.position;
            cameraForward = Camera.main.transform.forward;
        }
        else
        {
            return false;
        }

        Vector3 toHallucination = (transform.position - origin).normalized;
        Debug.DrawRay(origin, toHallucination * 5f, Color.yellow, 0.05f);
        float dot = Vector3.Dot(cameraForward, toHallucination);
        //Debug.Log($"[ShadowLogic] Dot: {dot}");
        return dot > 0.85f;
    }

    // Add this helper (near other helpers like IsPlayerLookingAtMe)
    private bool IsPlayerLineOfSightClear()
    {
        Vector3 origin;
        if (playerManager != null && playerManager.playerCamera != null)
        {
            origin = playerManager.playerCamera.transform.position;
        }
        else if (playerCameraLook != null)
        {
            origin = playerTransform != null ? playerTransform.position + Vector3.up * 1.6f : Vector3.zero;
        }
        else if (Camera.main != null)
        {
            origin = Camera.main.transform.position;
        }
        else
        {
            return false;
        }

        Vector3 toHallucination = transform.position - origin;
        float distance = toHallucination.magnitude;
        if (distance <= 0.01f) return true;

        // Only consider walls/obstacles as blocking. Adjust mask if you need additional blockers.
        int mask = LayerMask.GetMask("Walls", "Obstacles");
        RaycastHit hit;
        if (Physics.Raycast(origin, toHallucination.normalized, out hit, distance, mask))
        {
            // Something blocking the view (wall/obstacle) before reaching the hallucination
            return false;
        }

        return true;
    }

    private void HuntPlayer()
    {
        if (playerTransform == null || agent == null)
            return;

        // Move to a point at the edge of killRange (keep small buffer so it doesn't ram the player)
        Vector3 toPlayer = playerTransform.position - transform.position;
        float distToPlayer = toPlayer.magnitude;
        if (distToPlayer <= 0.01f)
        {
            // Already overlapping; just stop moving and keep visible
            agent.ResetPath();
            agent.isStopped = true;
            EnableRenderer();
            return;
        }

        Vector3 dir = toPlayer.normalized;
        float buffer = 0.5f;
        float desiredDistance = Mathf.Max(killRange - buffer, 0.5f);
        Vector3 target = playerTransform.position - dir * desiredDistance;

        // Snap target to navmesh if possible
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 2.0f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(target);

        agent.isStopped = false; // ensure agent follows path
        EnableRenderer();
    }

    // Modified StandardAIUpdate: count the reposition when agent is commanded to move
    private void StandardAIUpdate()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 1. Reposition if player is out of sight for 15 seconds
        bool currentlyVisible = CanSeePlayer();

        if (currentlyVisible)
        {
            outOfSightTimer = 0f;
            wasVisibleLastFrame = true;
        }
        else
        {
            if (wasVisibleLastFrame)
            {
                outOfSightTimer = 0f;
                wasVisibleLastFrame = false;
            }
            else
            {
                outOfSightTimer += Time.deltaTime;
                if (outOfSightTimer >= outOfSightThreshold)
                {
                    ScheduleReposition();
                    outOfSightTimer = 0f;
                    repositioningDueToproximity = false;
                }
            }
        }

        if (distanceToPlayer < repositionTriggerDistance)
        {
            float peekDestPlayerDist = Vector3.Distance(peekDestination, playerTransform.position);
            if (!repositioningDueToproximity || peekDestPlayerDist < repositionTriggerDistance)
            {
                ScheduleReposition();
                repositioningDueToproximity = true;
            }
        }
        else
        {
            repositioningDueToproximity = false;
        }

        // Move to peekDestination if not already there
        if (Vector3.Distance(transform.position, peekDestination) > 1.0f)
        {
            // Only count a reposition when we actually start moving (agent.SetDestination)
            if (repositionPending)
            {
                repositionPending = false;
                repositionsDone++;
                Debug.Log($"[ShadowLogic] Reposition #{repositionsDone} started.");

                if (repositionsDone > repositionsBeforeDestroy)
                {
                    Debug.Log("[ShadowLogic] Max repositions exceeded — despawning shadow before moving.");
                    Destroy(gameObject);
                    return;
                }
            }

            // If a reposition is scheduled (cooldown active), stay hidden and do not set destination yet.
            if (!repositionScheduled && !repositionLocked)
            {
                // Diagnostics: log agent state before issuing destination so we can see why it may not move.
                if (agent == null)
                {
                    Debug.LogWarning("[ShadowLogic] Agent is null when trying to SetDestination.");
                }
                else
                {
                    bool onNavMesh = true;
#if UNITY_2020_1_OR_NEWER
                        onNavMesh = agent.isOnNavMesh;
#endif
                    Debug.Log($"[ShadowLogic] Attempting SetDestination. enabled={agent.enabled}, onNavMesh={onNavMesh}, isStopped={agent.isStopped}, hasPath={agent.hasPath}, pathPending={agent.pathPending}, speed={agent.speed}, updatePosition={agent.updatePosition}");

                    // Safety: ensure agent is allowed to move
                    agent.isStopped = false;
                    agent.updatePosition = true;

                    // Prefer snapping target to NavMesh before setting destination
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(peekDestination, out hit, 2.0f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                        Debug.Log($"[ShadowLogic] SetDestination -> navHit at {hit.position}");
                    }
                    else
                    {
                        agent.SetDestination(peekDestination);
                        Debug.Log($"[ShadowLogic] SetDestination -> raw peekDestination {peekDestination} (NavMesh.SamplePosition failed)");
                    }

                    // Log result of attempting to set destination
                    Debug.Log($"[ShadowLogic] After SetDestination: hasPath={agent.hasPath}, pathPending={agent.pathPending}, remainingDistance={agent.remainingDistance}");
                }

                DisableRenderer();
            }
            else
            {
                // keep hidden until cooldown expires
                DisableRenderer();
            }
        }
        else
        {
            // only show and manage lights when not en route and not scheduled to hide
            if (!repositionScheduled)
            {
                EnableRenderer();
                ManageNearbyLights();
            }
            else
            {
                DisableRenderer();
            }
        }

        // Update the visual alpha of the shadow based on current sanity every frame
        UpdateShadowAlpha();
    }

    private void UpdateShadowVisibility()
    {
        // Sanity-based visibility:
        // alpha = 1 - (sanity - alphaLowSanityThreshold) / (alphaHighSanityThreshold - alphaLowSanityThreshold)
        // clamp alpha to 0.1 - 1 range
        float sanityFactor = Mathf.InverseLerp(alphaLowSanityThreshold, alphaHighSanityThreshold, playerStats.Sanity);
        sanityFactor = Mathf.Clamp01(sanityFactor);
        float targetAlpha = Mathf.Lerp(maxShadowAlpha, minShadowAlpha, sanityFactor);

        // Update all renderers
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>();
            cachedMaterials = new Material[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                cachedMaterials[i] = new Material(cachedRenderers[i].material);
                cachedRenderers[i].material = cachedMaterials[i];
            }
        }

        foreach (var rend in cachedRenderers)
        {
            foreach (var mat in rend.materials)
            {
                // Only modify our instance materials
                if (mat.name.Contains("ShadowMaterial"))
                {
                    // Smoothly interpolate the alpha value
                    Color color = mat.color;
                    color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * 5f);
                    mat.color = color;
                }
            }
        }
    }

    // Add this helper method to compute and apply alpha from player's sanity
    private void UpdateShadowAlpha()
    {
        if (cachedMaterials == null || cachedMaterials.Length == 0 || playerStats == null)
            return;

        // Normalized 0..1 where 0 => high sanity (alpha=min) and 1 => low sanity (alpha=max)
        float t = Mathf.InverseLerp(alphaHighSanityThreshold, alphaLowSanityThreshold, playerStats.Sanity);
        float alpha = Mathf.Lerp(minShadowAlpha, maxShadowAlpha, t);

        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            var mat = cachedMaterials[i];
            if (mat == null) continue;

            // Try common color properties. Fallback to _Color.
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
            else if (mat.HasProperty("_BaseColor")) // URP / HDRP
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            // If the shader does not use transparency, you must switch its render mode to Transparent externally.
        }
    }

    // Add this helper near other private helpers (e.g. next to CanSeePlayer)
    private bool IsMoving()
    {
        if (agent == null) return false;

        // Consider the shadow moving if:
        // - it has a path and a non-trivial velocity, or
        // - it has remaining distance larger than a small threshold.
        if (agent.pathPending) 
            return true; // path is being computed -> treat as moving

        if (agent.hasPath)
        {
            if (agent.velocity.sqrMagnitude > 0.01f) // moving by velocity
                return true;
            if (agent.remainingDistance > agent.stoppingDistance + 0.1f) // still en route
                return true;
        }

        return false;
    }

    // --- New helper: ScheduleReposition() ---
    // Add this method near other private helpers:

    private void ScheduleReposition()
    {
        if (repositionScheduled) return;

        // hide now, then pick new position after cooldown
        repositionScheduled = true;
        repositionLocked = true; // prevent SetDestination until cooldown ends
        repositionTimer = Mathf.Max(0.01f, repositionCooldownSeconds);

        // immediate visual disappearance
        DisableRenderer();

        // Restore lights immediately so lighting state isn't left dark while hidden
        RestoreLightsImmediately();

        if (playerStats != null)
            Debug.Log($"[ShadowLogic] Reposition scheduled in {repositionTimer:F1}s (repositionsDone={repositionsDone}/{repositionsBeforeDestroy}).");
    }
}
