using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Minimal event module responsible for spawning Shadow prefabs when sanity is low.
/// Uses EventManager inspector settings (threshold, cooldown, maxActive, chance curve).
/// </summary>
public class ShadowSpawnEventModule : IEventModule
{
    private float cooldownTimer = 0f;
    private float timeBelowThreshold = 0f;

    public ShadowSpawnEventModule(EventManager manager) : base(manager) { }

    public override void OnAwake()
    {
        cooldownTimer = 0f;
        timeBelowThreshold = 0f;
    }

    public override void OnUpdate()
    {
        if (manager == null) return;
        if (manager.shadowPrefab == null) return;

        // tick cooldown
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        float sanity = manager.playerSanity;
        float dt = Time.deltaTime;

        // Replace this line:
        // int activeShadows = Object.FindObjectsOfType<ShadowLogic>().Length;

        // With the following:
        int activeShadows = Object.FindObjectsByType<ShadowLogic>(FindObjectsSortMode.None).Length;
        if (activeShadows >= manager.shadowMaxActive)
        {
            // still accumulate time while more shadows are active so chance ramps when they disappear
            if (sanity < manager.shadowSpawnSanityThreshold)
                timeBelowThreshold += dt;
            else
                timeBelowThreshold = 0f;
            return;
        }

        if (sanity < manager.shadowSpawnSanityThreshold)
        {
            timeBelowThreshold += dt;

            // sanity factor: how far below threshold (0..1)
            float sanityFactor = Mathf.Clamp01((manager.shadowSpawnSanityThreshold - sanity) / Mathf.Max(0.0001f, manager.shadowSpawnSanityThreshold));
            // time factor: 0..1 over configured ramp time
            float timeFactor = Mathf.Clamp01(timeBelowThreshold / Mathf.Max(0.0001f, manager.shadowTimeToMax));

            float chancePerSecond = manager.shadowMaxChance * sanityFactor * timeFactor;

            // if on cooldown, don't attempt spawn
            if (cooldownTimer <= 0f)
            {
                float roll = chancePerSecond * dt;
                if (Random.value < roll)
                {
                    // Spawn attempt
                    if (TrySpawnShadow())
                    {
                        cooldownTimer = manager.shadowSpawnCooldownSeconds;
                        timeBelowThreshold = 0f;
                    }
                }
            }
        }
        else
        {
            timeBelowThreshold = 0f;
        }
    }

    // Attempts to find a NavMesh position near the player and instantiate the shadow prefab.
    private bool TrySpawnShadow()
    {
        Transform playerT = null;
        if (manager.playerManager != null)
            playerT = manager.playerManager.transform;
        if (playerT == null && PlayerStats.Instance != null)
            playerT = PlayerStats.Instance.transform;
        if (playerT == null)
            playerT = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (playerT == null)
        {
            Debug.LogWarning("[ShadowSpawnEventModule] No player transform found to spawn shadow around.");
            return false;
        }

        // Try several samples around the player
        const int attempts = 12;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(manager.shadowSpawnRadius * 0.5f, manager.shadowSpawnRadius);
            Vector3 candidate = playerT.position + new Vector3(circle.x, 0f, circle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 6f, NavMesh.AllAreas))
            {
                Object.Instantiate(manager.shadowPrefab, hit.position, Quaternion.identity);
                Debug.Log($"[ShadowSpawnEventModule] Spawned shadow at {hit.position}");
                return true;
            }
        }

        Debug.LogWarning("[ShadowSpawnEventModule] Failed to find NavMesh position to spawn shadow.");
        return false;
    }
}
