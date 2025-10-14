using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class ShadowLogic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private float minMoveDistance = 8f;
    [SerializeField] private float minPlayerDistance = 20f;
    [SerializeField] private float lightDisableRadius = 10f;
    [SerializeField] private int sampleCount = 32;
    [SerializeField] private float searchRadius = 20f;
    [SerializeField] private float killRange = 10f; // Set as needed
    [SerializeField] private float sanityDrainPerSecond = 5f; // Use float, not int

    private Vector3 peekDestination;
    private bool isVisible = false;
    private HashSet<Light> disabledLights = new HashSet<Light>();

    private float outOfSightTimer = 0f;
    private const float outOfSightThreshold = 15f;

    private bool repositioningDueToproximity = false;
    private bool wasVisibleLastFrame = true;

    private PlayerStats playerStats;
    private PlayerManager playerManager;
    private PlayerCameraLook playerCameraLook;
    private bool isHunting = false;

    private bool isAttacking = false;
    private float attackWindupTimer = 0f;
    private const float attackWindupDuration = 0.5f;

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
        FindPeekPosition();

        playerStats = Object.FindFirstObjectByType<PlayerStats>();

        // Get PlayerManager from playerTransform
        playerManager = playerTransform != null ? playerTransform.GetComponent<PlayerManager>() : null;
        playerCameraLook = playerManager != null ? playerManager.cameraLookModule : null;
    }

    private void Update()
    {
        if (playerTransform == null || agent == null || playerStats == null || playerCameraLook == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Sanity drain if player is looking at hallucination (no distance check)
        if (IsPlayerLookingAtMe())
        {
            playerStats.ChangeSanity(-sanityDrainPerSecond * Time.deltaTime);
            Debug.Log("[ShadowLogic] Draining sanity! " + playerStats.Sanity);
        }

        // HUNT LOGIC: If sanity < 90, only hunt/attack, no repositioning or avoidance
        if (playerStats.Sanity < 90)
        {
            if (!isHunting)
            {
                isHunting = true;
                Debug.Log("[ShadowLogic] HUNT MODE: Player is being hunted!");
            }

            HuntPlayer(); // Keeps distance for attack, but does NOT avoid player

            // Attack logic
            if (distanceToPlayer <= killRange)
            {
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
                        }
                        else
                        {
                            Debug.Log("[ShadowLogic] Attack missed, player moved away!");
                        }
                        isAttacking = false;
                        attackWindupTimer = 0f;
                    }
                }
            }
            else
            {
                if (isAttacking)
                {
                    Debug.Log("[ShadowLogic] Attack cancelled, player moved out of range.");
                    isAttacking = false;
                    attackWindupTimer = 0f;
                }
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
            Debug.Log("[ShadowLogic] Repositioning: Player sanity recovered.");
        }
        StandardAIUpdate(); // Only runs when not hunting
    }

    private void FindPeekPosition()
    {
        Vector3 bestSafePosition = transform.position;
        float bestSafeScore = float.MaxValue;
        bool foundSafe = false;

        Vector3 farthestVisiblePosition = transform.position;
        float farthestPlayerDist = 0f;
        bool foundVisible = false;

        Vector3 fallbackPosition = transform.position;
        float maxPlayerDist = 0f;

        int candidatesTested = 0;
        int candidatesVisible = 0;
        int candidatesNavMesh = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            float angle = i * (360f / sampleCount);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 candidate = transform.position + dir * Random.Range(minMoveDistance, searchRadius);

            float moveDist = Vector3.Distance(candidate, transform.position);
            float playerDist = Vector3.Distance(candidate, playerTransform.position);

            if (moveDist < minMoveDistance) continue;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, 2.0f, NavMesh.AllAreas))
            {
                candidatesNavMesh++;
                bool canSee = CanSeePlayerFromPosition(hit.position);
                candidatesTested++;
                if (canSee) candidatesVisible++;

                if (canSee && playerDist >= minPlayerDistance)
                {
                    float score = playerDist + moveDist;
                    if (score < bestSafeScore)
                    {
                        bestSafeScore = score;
                        bestSafePosition = hit.position;
                        foundSafe = true;
                    }
                }
                else if (canSee)
                {
                    if (playerDist > farthestPlayerDist)
                    {
                        farthestPlayerDist = playerDist;
                        farthestVisiblePosition = hit.position;
                        foundVisible = true;
                    }
                }
                if (playerDist > maxPlayerDist)
                {
                    maxPlayerDist = playerDist;
                    fallbackPosition = hit.position;
                }
            }
        }

        Debug.Log($"[ShadowLogic] Candidates tested: {candidatesTested}, NavMesh: {candidatesNavMesh}, Visible: {candidatesVisible}");

        if (foundSafe)
        {
            peekDestination = bestSafePosition;
            Debug.Log("[ShadowLogic] Moving to safe visible position.");
        }
        else if (foundVisible)
        {
            peekDestination = farthestVisiblePosition;
            Debug.LogWarning("[ShadowLogic] No safe position found, moving to farthest visible position.");
        }
        else if (candidatesNavMesh > 0)
        {
            peekDestination = fallbackPosition;
            Debug.LogWarning("[ShadowLogic] No visible position found, moving to farthest possible position.");
        }
        else
        {
            Debug.LogWarning("[ShadowLogic] No valid stalk position found, staying put.");
            peekDestination = transform.position;
        }
    }

    // Helper: checks if player is visible from a given position
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

    private void ManageNearbyLights()
    {
        var lights = FindObjectsOfType<Light>();
        foreach (var light in lights)
        {
            float dist = Vector3.Distance(transform.position, light.transform.position);

            if (dist <= lightDisableRadius)
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    disabledLights.Add(light);
                }
            }
            else
            {
                if (disabledLights.Contains(light))
                {
                    light.enabled = true;
                    disabledLights.Remove(light);
                }
            }
        }
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

    private bool IsPlayerLookingAtMe()
    {
        if (playerStats == null || playerCameraLook == null) return false;
        Vector3 toHallucination = (transform.position - playerStats.transform.position).normalized;
        Vector3 cameraForward = playerCameraLook.GetCameraForward();
        float dot = Vector3.Dot(cameraForward, toHallucination);
        // Debug log to help you tune the threshold
        Debug.Log($"[ShadowLogic] Dot: {dot}");
        return dot > 0.85f; // Lower threshold for easier detection
    }

    private void HuntPlayer()
    {
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        Vector3 targetPosition = playerTransform.position - directionToPlayer * (killRange - 0.5f);
        agent.SetDestination(targetPosition);
        EnableRenderer();
    }

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
                    FindPeekPosition();
                    outOfSightTimer = 0f;
                    repositioningDueToproximity = false;
                }
            }
        }

        // 2. Reposition once if player is too close
        if (distanceToPlayer < minPlayerDistance)
        {
            if (!repositioningDueToproximity)
            {
                FindPeekPosition();
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
            agent.SetDestination(peekDestination);
            DisableRenderer();
        }
        else
        {
            EnableRenderer();
            ManageNearbyLights();
        }
    }
}
