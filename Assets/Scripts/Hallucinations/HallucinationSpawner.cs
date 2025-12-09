using UnityEngine;
using UnityEngine.AI;

public class HallucinationSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] hallucinationPrefabs;

    [Header("Spawn settings")]
    [Tooltip("Minimum distance from player to spawn")]
    [SerializeField] private float minSpawnDistance = 30f;
    [Tooltip("Maximum distance from player to spawn")]
    [SerializeField] private float maxSpawnDistance = 60f;
    [Tooltip("Player sanity threshold (spawn when below)")]
    [SerializeField] private float spawnSanityThreshold = 90f;
    [Tooltip("Time in seconds before a new hallucination can spawn after the previous dies")]
    [SerializeField] private float respawnCooldown = 10f;

    private PlayerStats playerStats;
    private Transform playerTransform;

    // runtime state
    private GameObject currentHallucination;
    private float cooldownTimer = 0f;
    private bool hasSpawnedOnce = false;
    private bool wasBelowThresholdLastFrame = false;

    private void Awake()
    {
        playerStats = PlayerStats.Instance != null ? PlayerStats.Instance : FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
            playerTransform = playerStats.transform;
        // else try to find player transform by tag as fallback
        if (playerTransform == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    private void Update()
    {
        // basic guards
        if (playerTransform == null || hallucinationPrefabs == null || hallucinationPrefabs.Length == 0 || playerStats == null)
            return;

        // cooldown tick
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        bool below = playerStats.Sanity < spawnSanityThreshold;

        // If there's an active hallucination, keep tracking; when it becomes null (destroyed), start cooldown
        if (currentHallucination != null)
        {
            if (currentHallucination == null) // Unity-null check after destroy
            {
                currentHallucination = null;
                cooldownTimer = respawnCooldown;
            }
            // still alive -> nothing else
            wasBelowThresholdLastFrame = below;
            return;
        }

        // No active hallucination: decide whether to spawn
        if (cooldownTimer > 0f)
        {
            // waiting for cooldown
            wasBelowThresholdLastFrame = below;
            return;
        }

        // First spawn should happen on the moment sanity dips below threshold (edge detect)
        if (!hasSpawnedOnce)
        {
            if (below && !wasBelowThresholdLastFrame)
            {
                TrySpawnRandomHallucination();
                hasSpawnedOnce = true;
            }
        }
        else
        {
            // subsequent spawns: if sanity still low and cooldown expired, spawn automatically
            if (below)
            {
                TrySpawnRandomHallucination();
            }
        }

        wasBelowThresholdLastFrame = below;
    }

    private void TrySpawnRandomHallucination()
    {
        if (hallucinationPrefabs == null || hallucinationPrefabs.Length == 0)
            return;

        // pick random prefab
        var prefab = hallucinationPrefabs[Random.Range(0, hallucinationPrefabs.Length)];
        if (prefab == null)
            return;

        // try several attempts to find a valid NavMesh position within annulus
        const int attempts = 16;
        for (int i = 0; i < attempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 candidate = playerTransform.position + new Vector3(circle.x, 0f, circle.y);

            // sample navmesh near candidate
            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 8f, NavMesh.AllAreas))
            {
                // instantiate at nav position
                currentHallucination = Instantiate(prefab, hit.position, Quaternion.identity);
                Debug.Log($"[HallucinationSpawner] Spawned {prefab.name} at {hit.position}");
                return;
            }
        }

        Debug.LogWarning("[HallucinationSpawner] Failed to find NavMesh position for hallucination spawn after attempts.");
    }

    // Optional: public helper to force spawn (useful for debugging)
    public void ForceSpawn()
    {
        cooldownTimer = 0f;
        hasSpawnedOnce = true;
        TrySpawnRandomHallucination();
    }
}
