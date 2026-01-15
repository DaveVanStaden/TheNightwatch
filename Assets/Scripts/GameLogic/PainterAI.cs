using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple AI for the Painter enemy that chases the player.
/// Uses NavMeshAgent for pathfinding.
/// When it touches the player, triggers game over.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PainterAI : MonoBehaviour
{
    [Header("Chase Settings")]
    [Tooltip("If true, the painter actively chases the player")]
    [SerializeField] private bool chaseActive = false;
    [Tooltip("Movement speed when chasing")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [Tooltip("How close the painter needs to be to catch the player")]
    [SerializeField] private float catchDistance = 1.5f;
    [Tooltip("How close the painter will get before stopping (should be slightly less than catchDistance)")]
    [SerializeField] private float stoppingDistance = 1.2f;

    [Header("References")]
    [Tooltip("The player transform to chase (will auto-find if null)")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("NavMeshAgent component (auto-assigned)")]
    private NavMeshAgent agent;
    [Tooltip("Reference to FinalSequenceManager")]
    private FinalSequenceManager sequenceManager;

    [Header("Audio (Optional)")]
    [Tooltip("Audio source for chase sounds/footsteps")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Sound to play when chase starts")]
    [SerializeField] private AudioClip chaseStartSound;
    [Tooltip("Looping chase music/breathing sound")]
    [SerializeField] private AudioClip chaseLoopSound;

    [Header("Visual (Optional)")]
    [Tooltip("Animator for painter animations")]
    [SerializeField] private Animator animator;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool playerCaught = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        sequenceManager = FindAnyObjectByType<FinalSequenceManager>();

        // Find player if not assigned
        if (playerTarget == null)
        {
            var playerManager = FindAnyObjectByType<PlayerManager>();
            if (playerManager != null)
                playerTarget = playerManager.transform;
            else
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTarget = player.transform;
            }
        }

        // Configure NavMeshAgent
        if (agent != null)
        {
            agent.speed = chaseSpeed;
            agent.stoppingDistance = stoppingDistance; // Stop before reaching player
            agent.acceleration = 8f; // Smooth acceleration
            agent.angularSpeed = 120f; // Smooth turning
            agent.autoBraking = true; // Slow down when approaching destination
            agent.enabled = false; // Disabled until chase starts
            
            if (debugLogs)
            {
                Debug.Log($"[PainterAI] NavMeshAgent configured - Speed:{chaseSpeed}, StoppingDist:{stoppingDistance}, CatchDist:{catchDistance}");
            }
        }
    }

    private void Update()
    {
        if (!chaseActive || playerCaught || playerTarget == null || agent == null)
            return;

        // Update destination to player position
        if (agent.enabled && agent.isOnNavMesh)
        {
            // Only update destination if player has moved significantly
            float distanceToDestination = Vector3.Distance(agent.destination, playerTarget.position);
            if (distanceToDestination > 0.5f)
            {
                agent.SetDestination(playerTarget.position);
            }

            // NOTE: No animator parameters to update - only triggers "Spawn" and "Walk" exist
            // Movement animation should be handled by the animator's state machine based on velocity
        }

        // Check if close enough to catch player
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        if (distanceToPlayer <= catchDistance)
        {
            CatchPlayer();
        }
    }

    /// <summary>
    /// Enable or disable chase mode
    /// </summary>
    public void SetChaseActive(bool active)
    {
        chaseActive = active;

        if (agent != null)
            agent.enabled = active;

        if (active)
        {
            if (debugLogs) Debug.Log("[PainterAI] Chase activated!");

            // Play chase start sound
            if (audioSource != null && chaseStartSound != null)
            {
                audioSource.PlayOneShot(chaseStartSound);
            }

            // Start looping chase sound
            if (audioSource != null && chaseLoopSound != null)
            {
                audioSource.clip = chaseLoopSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            // NOTE: Animation triggers are handled by FinalSequenceManager
            // We don't set any animator parameters here since only "Spawn" and "Walk" triggers exist
        }
        else
        {
            if (debugLogs) Debug.Log("[PainterAI] Chase deactivated");

            // Stop sounds
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            // NOTE: No animator parameters to set - triggers are one-time only
        }
    }

    /// <summary>
    /// Called when painter catches the player
    /// </summary>
    private void CatchPlayer()
    {
        if (playerCaught) return;

        playerCaught = true;
        if (debugLogs) Debug.Log("[PainterAI] Player caught!");

        // Stop movement
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // NOTE: No "Catch" trigger exists - add one if needed for catch animation
        // Catch animation could be handled through other means (Timeline, manual animation, etc.)

        // Notify sequence manager
        if (sequenceManager != null)
        {
            sequenceManager.OnPlayerCaught();
        }
        else
        {
            Debug.LogWarning("[PainterAI] FinalSequenceManager not found!");
        }
    }

    /// <summary>
    /// Alternative collision-based detection (if NavMesh distance check isn't enough)
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (playerCaught || !chaseActive) return;

        // Check if it's the player
        if (other.CompareTag("Player") || other.GetComponent<PlayerManager>() != null)
        {
            CatchPlayer();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (playerCaught || !chaseActive) return;

        // Check if it's the player
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<PlayerManager>() != null)
        {
            CatchPlayer();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize catch distance in editor
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);

        // Draw line to player
        if (playerTarget != null)
        {
            Gizmos.color = chaseActive ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }
}
