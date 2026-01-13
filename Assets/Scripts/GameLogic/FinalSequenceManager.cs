using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the final sequence that triggers after all tasks are completed.
/// Handles painter spawn, chase mechanics, environmental changes, and end sequence.
/// Designer-friendly with inspector lists for flexible configuration.
/// </summary>
public class FinalSequenceManager : MonoBehaviour
{
    [Header("Sequence State")]
    [Tooltip("If true, the final sequence has been activated")]
    public bool sequenceActivated = false;
    [Tooltip("If true, the painter is actively chasing the player")]
    public bool chaseActive = false;
    [Tooltip("If true, all tasks are complete and sequence is ready to activate (waiting for player to enter office trigger)")]
    public bool sequenceReady = false;

    [Header("Painter AI")]
    [Tooltip("The painter AI GameObject (will be spawned/activated when sequence starts)")]
    [SerializeField] private GameObject painterPrefab;
    [Tooltip("Optional: Spawn position for the painter. If null, uses this GameObject's position")]
    [SerializeField] private Transform painterSpawnPoint;
    [Tooltip("Reference to the spawned painter (set at runtime)")]
    private GameObject painterInstance;

    [Header("Activation Trigger")]
    [Tooltip("GameObject with trigger collider - sequence will activate when player enters this (e.g., office area)")]
    [SerializeField] private GameObject activationTrigger;

    [Header("Initial Spawn Objects (Instant)")]
    [Tooltip("Doors/walls that spawn/activate instantly when sequence activates (to guide player)")]
    [SerializeField] private List<GameObject> guidanceObjectsToSpawn = new List<GameObject>();
    [Tooltip("Ink-related models that spawn/activate instantly when sequence activates")]
    [SerializeField] private List<GameObject> inkObjectsToSpawn = new List<GameObject>();

    [Header("Chase Trigger Wall")]
    [Tooltip("Wall GameObject with BoxCollider (trigger) that starts the chase when player enters")]
    [SerializeField] private GameObject chaseWall;
    [Tooltip("Objects that spawn/activate when chase begins (blocking objects)")]
    [SerializeField] private List<GameObject> chaseBlockingObjects = new List<GameObject>();

    [Header("End Sequence Trigger Wall")]
    [Tooltip("Wall GameObject with BoxCollider (trigger) that starts the end sequence when player enters")]
    [SerializeField] private GameObject endSequenceWall;

    [Header("Events - Sequence Start")]
    [Tooltip("Called immediately when final sequence activates (before any spawns)")]
    public UnityEvent onSequenceStart;
    [Tooltip("Called after all initial objects have spawned")]
    public UnityEvent onInitialSpawnComplete;

    [Header("Events - Chase")]
    [Tooltip("Called when player triggers the chase wall")]
    public UnityEvent onChaseStart;
    [Tooltip("Called when painter catches the player")]
    public UnityEvent onPlayerCaught;

    [Header("Events - End Sequence")]
    [Tooltip("Called when player reaches the end sequence trigger")]
    public UnityEvent onEndSequenceStart;
    [Tooltip("Called when end sequence completes (if you have multiple steps)")]
    public UnityEvent onEndSequenceComplete;

    [Header("References")]
    [Tooltip("Reference to TaskManager to check if all tasks completed")]
    [SerializeField] private TaskManager taskManager;
    [Tooltip("Optional: End menu to show when player is caught")]
    [SerializeField] private GameObject gameOverScreen;

    // Reference to shadow spawner for disabling during finale
    private HallucinationSpawner shadowSpawner;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [Tooltip("DEBUG: If true, the finale sequence will activate immediately on Start (skips all task requirements)")]
    [SerializeField] private bool debugForceSequenceOnStart = false;

    // Reference to the finale task
    private FinaleTask finaleTask;
    
    // Track last check state to avoid log spam
    private int lastCompletedCount = -1;

    // Cutscene state tracking
    private bool cutscenePlaying = false;
    private PlayerManager cachedPlayerManager;

    private void Start()
    {
        // Find TaskManager if not assigned
        if (taskManager == null)
            taskManager = FindAnyObjectByType<TaskManager>();

        // Find shadow spawner
        shadowSpawner = FindAnyObjectByType<HallucinationSpawner>();
        if (debugLogs)
        {
            if (shadowSpawner != null)
                Debug.Log("[FinalSequence] Found HallucinationSpawner");
            else
                Debug.LogWarning("[FinalSequence] HallucinationSpawner not found");
        }

        // Reset sequence state on scene start
        ResetSequence();

        // Ensure all spawn objects are initially inactive
        DeactivateAllSpawnObjects();

        // Setup wall triggers
        SetupWallTriggers();

        // Setup activation trigger
        SetupActivationTrigger();

        // DEBUG: Force sequence activation if debug flag is enabled
        if (debugForceSequenceOnStart)
        {
            Debug.LogWarning("[FinalSequence] DEBUG MODE: Force activating finale sequence on Start!");
            sequenceReady = true;
            ActivateFinalSequence();
        }
    }

    private void Update()
    {
        // Check if all tasks are completed and sequence hasn't been marked ready yet
        if (!sequenceReady && !sequenceActivated && AreAllTasksCompleted())
        {
            MarkSequenceReady();
        }
    }

    /// <summary>
    /// Mark the sequence as ready to activate (tasks complete, waiting for player to enter office)
    /// </summary>
    private void MarkSequenceReady()
    {
        sequenceReady = true;
        if (debugLogs) Debug.Log("[FinalSequence] All tasks complete! Sequence is READY. Waiting for player to enter office trigger...");
        
        // Enable the activation trigger
        if (activationTrigger != null)
        {
            activationTrigger.SetActive(true);
            if (debugLogs) Debug.Log($"[FinalSequence] Activation trigger '{activationTrigger.name}' enabled");
        }
    }

    /// <summary>
    /// Setup BoxCollider triggers on the wall objects
    /// </summary>
    private void SetupWallTriggers()
    {
        // Setup chase wall
        if (chaseWall != null)
        {
            SetupWallCollider(chaseWall, OnChaseWallTriggered);
            chaseWall.SetActive(false); // Start inactive until sequence begins
        }

        // Setup end sequence wall
        if (endSequenceWall != null)
        {
            SetupWallCollider(endSequenceWall, OnEndSequenceWallTriggered);
            endSequenceWall.SetActive(false); // Start inactive until sequence begins
        }
    }

    /// <summary>
    /// Setup the activation trigger that starts the sequence when player enters office
    /// </summary>
    private void SetupActivationTrigger()
    {
        if (activationTrigger != null)
        {
            SetupWallCollider(activationTrigger, OnActivationTriggerEntered);
            activationTrigger.SetActive(false); // Start inactive until tasks are complete
            if (debugLogs) Debug.Log($"[FinalSequence] Activation trigger '{activationTrigger.name}' setup complete");
        }
        else
        {
            Debug.LogWarning("[FinalSequence] Activation trigger not assigned! Sequence will activate immediately when tasks complete.");
        }
    }

    /// <summary>
    /// Called when player enters the activation trigger (office area)
    /// </summary>
    private void OnActivationTriggerEntered(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (!sequenceReady || sequenceActivated) return;

        if (debugLogs) Debug.Log("[FinalSequence] Player entered activation trigger - starting finale sequence!");
        ActivateFinalSequence();
    }

    /// <summary>
    /// Ensure wall has a BoxCollider set to trigger and add trigger handler
    /// </summary>
    private void SetupWallCollider(GameObject wall, System.Action<Collider> onTriggerCallback)
    {
        var collider = wall.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = wall.AddComponent<BoxCollider>();
            if (debugLogs) Debug.Log($"[FinalSequence] Added BoxCollider to {wall.name}");
        }

        if (!collider.isTrigger)
        {
            collider.isTrigger = true;
            if (debugLogs) Debug.Log($"[FinalSequence] Set {wall.name} collider to trigger");
        }

        // Add trigger handler component
        var triggerHandler = wall.GetComponent<WallTriggerHandler>();
        if (triggerHandler == null)
        {
            triggerHandler = wall.AddComponent<WallTriggerHandler>();
        }
        triggerHandler.Initialize(onTriggerCallback);
    }

    /// <summary>
    /// Called when player enters chase wall
    /// </summary>
    private void OnChaseWallTriggered(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (!sequenceActivated || chaseActive || cutscenePlaying) return;

        // Get player manager reference
        cachedPlayerManager = other.GetComponent<PlayerManager>();
        if (cachedPlayerManager == null)
            cachedPlayerManager = other.GetComponentInParent<PlayerManager>();

        // Start cutscene instead of chase directly
        StartCoroutine(CutsceneCoroutine());
    }

    /// <summary>
    /// Called when player enters end sequence wall
    /// </summary>
    private void OnEndSequenceWallTriggered(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (!sequenceActivated) return;

        StartEndSequence();
    }

    /// <summary>
    /// Check if collider belongs to player
    /// </summary>
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || 
               other.GetComponent<PlayerManager>() != null ||
               other.GetComponentInParent<PlayerManager>() != null;
    }

    /// <summary>
    /// Check if all required tasks have been completed
    /// </summary>
    private bool AreAllTasksCompleted()
    {
        if (taskManager == null)
        {
            if (debugLogs) Debug.LogWarning("[FinalSequence] TaskManager is null!");
            return false;
        }

        // Check for the 3 specific tasks:
        // 1. SinglePaintingFallTask (tutorial)
        // 2. PaintingTask (paintings falling)
        // 3. TrashTask (trash cleanup)
        
        bool tutorialComplete = taskManager.completedTasks.Contains("SinglePaintingFallTask") || 
                                taskManager.completedTasks.Contains("SinglePaintingFall");
        bool paintingComplete = taskManager.completedTasks.Contains("PaintingTask");
        bool trashComplete = taskManager.completedTasks.Contains("TrashTask");

        // Only log when completed count changes to avoid spam
        if (debugLogs && taskManager.completedTasks.Count != lastCompletedCount)
        {
            lastCompletedCount = taskManager.completedTasks.Count;
            
            if (taskManager.completedTasks.Count > 0)
            {
                Debug.Log($"[FinalSequence] Completed tasks ({taskManager.completedTasks.Count}): {string.Join(", ", taskManager.completedTasks)}");
            }
            
            Debug.Log($"[FinalSequence] Task status - Tutorial:{tutorialComplete}, Painting:{paintingComplete}, Trash:{trashComplete}");
        }

        return tutorialComplete && paintingComplete && trashComplete;
    }

    /// <summary>
    /// Activate the final sequence (called automatically when all tasks complete)
    /// </summary>
    public void ActivateFinalSequence()
    {
        if (sequenceActivated) return;

        sequenceActivated = true;
        if (debugLogs) Debug.Log("[FinalSequence] FINAL SEQUENCE ACTIVATED!");

        // IMPORTANT: Disable shadow spawning permanently for the finale
        if (shadowSpawner != null)
        {
            shadowSpawner.SetSpawningEnabled(false);
            if (debugLogs) Debug.Log("[FinalSequence] Shadow spawning DISABLED for finale");
        }

        // Trigger designer events
        onSequenceStart?.Invoke();

        // Create and activate finale task in TaskManager
        if (taskManager != null)
        {
            finaleTask = new FinaleTask();
            finaleTask.Initialize(taskManager);
            
            // Add to task manager's task list
            if (!taskManager.tasks.Contains(finaleTask))
            {
                taskManager.tasks.Add(finaleTask);
            }
            
            // Activate the finale task
            if (finaleTask.CanActivate(null))
            {
                finaleTask.Activate(null);
                taskManager.AddActiveTask(finaleTask);
            }
        }

        // Spawn painter
        SpawnPainter();

        // Activate guidance objects (doors/walls to guide player)
        ActivateObjects(guidanceObjectsToSpawn, "Guidance");

        // Activate ink objects
        ActivateObjects(inkObjectsToSpawn, "Ink");

        // Activate chase wall trigger (but NOT end sequence wall yet)
        if (chaseWall != null) chaseWall.SetActive(true);
        // End sequence wall will activate when chase starts

        // Trigger completion event
        onInitialSpawnComplete?.Invoke();

        if (debugLogs) Debug.Log("[FinalSequence] Initial spawn complete. Waiting for chase trigger...");
    }

    /// <summary>
    /// Spawn the painter AI
    /// </summary>
    private void SpawnPainter()
    {
        if (painterPrefab == null)
        {
            Debug.LogWarning("[FinalSequence] Painter prefab not assigned!");
            return;
        }

        Vector3 spawnPos = painterSpawnPoint != null ? painterSpawnPoint.position : transform.position;
        Quaternion spawnRot = painterSpawnPoint != null ? painterSpawnPoint.rotation : Quaternion.identity;

        painterInstance = Instantiate(painterPrefab, spawnPos, spawnRot);
        painterInstance.name = "Painter_AI";

        // Painter should start inactive/idle until chase triggers
        var painterAI = painterInstance.GetComponent<PainterAI>();
        if (painterAI != null)
        {
            painterAI.SetChaseActive(false);
        }

        // No need to initialize animator - it will be triggered by the cutscene
        if (debugLogs) Debug.Log("[FinalSequence] Painter spawned (animator will be triggered during cutscene)");

        // Disable the painter GameObject until chase trigger is entered
        // This prevents it from being visible before the cutscene
        painterInstance.SetActive(false);
        if (debugLogs) Debug.Log($"[FinalSequence] Painter spawned at {spawnPos} and set to inactive (will activate on chase trigger)");
    }

    /// <summary>
    /// Activate a list of GameObjects
    /// </summary>
    private void ActivateObjects(List<GameObject> objects, string categoryName)
    {
        if (objects == null || objects.Count == 0) return;

        int activatedCount = 0;
        foreach (var obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                activatedCount++;
            }
        }

        if (debugLogs) Debug.Log($"[FinalSequence] Activated {activatedCount} {categoryName} objects");
    }

    /// <summary>
    /// Deactivate all spawn objects at start
    /// </summary>
    private void DeactivateAllSpawnObjects()
    {
        foreach (var obj in guidanceObjectsToSpawn)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in inkObjectsToSpawn)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in chaseBlockingObjects)
            if (obj != null) obj.SetActive(false);
    }

    /// <summary>
    /// Cutscene coroutine that freezes player and plays the Timeline cutscene
    /// </summary>
    private IEnumerator CutsceneCoroutine()
    {
        cutscenePlaying = true;
        if (debugLogs) Debug.Log("[FinalSequence] CUTSCENE STARTED - Freezing player");

        // Freeze player
        FreezePlayer(true);

        // Activate the painter GameObject (it was inactive since spawn)
        if (painterInstance != null)
        {
            painterInstance.SetActive(true);
            if (debugLogs) Debug.Log("[FinalSequence] Painter GameObject activated for cutscene");
        }

        // Get the PlayableDirector from the painter
        UnityEngine.Playables.PlayableDirector playableDirector = null;
        if (painterInstance != null)
        {
            playableDirector = painterInstance.GetComponent<UnityEngine.Playables.PlayableDirector>();
            
            if (playableDirector != null)
            {
                // Play the cutscene Timeline
                playableDirector.Play();
                if (debugLogs) Debug.Log($"[FinalSequence] Playing cutscene Timeline. Duration: {playableDirector.duration}s");
                
                // Wait for the cutscene to complete
                while (playableDirector.state == UnityEngine.Playables.PlayState.Playing)
                {
                    yield return null;
                }
                
                if (debugLogs) Debug.Log("[FinalSequence] Cutscene Timeline finished");
            }
            else
            {
                Debug.LogWarning("[FinalSequence] Painter has no PlayableDirector component! Cutscene will not play.");
                // Still wait a bit so it doesn't feel instant
                yield return new WaitForSeconds(1f);
            }
        }
        else
        {
            Debug.LogError("[FinalSequence] Painter instance is null, cannot play cutscene!");
            yield return new WaitForSeconds(1f);
        }

        // Unfreeze player
        FreezePlayer(false);

        cutscenePlaying = false;
        if (debugLogs) Debug.Log("[FinalSequence] CUTSCENE ENDED - Starting chase");

        // Trigger "Spawn" animation on painter AFTER cutscene ends (if it wasn't handled by Timeline)
        // This can be used to transition from cutscene pose to idle/ready pose
        if (painterInstance != null)
        {
            Animator animator = GetPainterAnimator();
            if (animator != null)
            {
                animator.SetTrigger("Spawn");
                if (debugLogs) Debug.Log("[FinalSequence] Triggered 'Spawn' animation on Painter (post-cutscene)");
            }
            else
            {
                Debug.LogWarning("[FinalSequence] No Animator found to trigger Spawn animation!");
            }
        }

        // Now start the actual chase
        StartChase();
    }

    /// <summary>
    /// Freeze or unfreeze the player (movement, camera, interactions)
    /// </summary>
    private void FreezePlayer(bool freeze)
    {
        if (cachedPlayerManager == null)
        {
            Debug.LogWarning("[FinalSequence] Cannot freeze player - PlayerManager not found");
            return;
        }

        if (freeze)
        {
            // Disable PlayerManager to stop all movement and input
            cachedPlayerManager.enabled = false;

            // Disable camera rotation (pause it for the cutscene duration)
            cachedPlayerManager.PauseCamera();

            if (debugLogs) Debug.Log("[FinalSequence] Player frozen");
        }
        else
        {
            // Re-enable PlayerManager
            cachedPlayerManager.enabled = true;

            // Re-enable camera
            cachedPlayerManager.ResumeCamera();

            if (debugLogs) Debug.Log("[FinalSequence] Player unfrozen");
        }
    }

    /// <summary>
    /// Helper method to get the Animator from the Painter (checks child "Painter Animated" first)
    /// </summary>
    private Animator GetPainterAnimator()
    {
        if (painterInstance == null) return null;

        // First, try to find the "Painter Animated" child
        Transform painterAnimated = painterInstance.transform.Find("Painter Animated");
        if (painterAnimated != null)
        {
            Animator animator = painterAnimated.GetComponent<Animator>();
            if (animator != null)
            {
                return animator;
            }
        }

        // Fallback: search in all children
        Animator fallbackAnimator = painterInstance.GetComponentInChildren<Animator>();
        if (fallbackAnimator != null && debugLogs)
        {
            Debug.LogWarning($"[FinalSequence] 'Painter Animated' child not found, using animator from: {fallbackAnimator.gameObject.name}");
        }

        return fallbackAnimator;
    }

    /// <summary>
    /// Called when player enters the chase wall trigger
    /// </summary>
    public void StartChase()
    {
        if (!sequenceActivated || chaseActive) return;

        chaseActive = true;
        if (debugLogs) Debug.Log("[FinalSequence] CHASE STARTED!");

        // Trigger "Walk" animation on painter
        // Use a small delay to ensure Spawn animation can finish if it's playing
        StartCoroutine(TriggerWalkAnimationDelayed());

        // Trigger designer event
        onChaseStart?.Invoke();

        // Activate painter AI chase
        if (painterInstance != null)
        {
            var painterAI = painterInstance.GetComponent<PainterAI>();
            if (painterAI != null)
            {
                painterAI.SetChaseActive(true);
            }
        }

        // Spawn blocking objects
        ActivateObjects(chaseBlockingObjects, "Chase Blocking");

        // NOW activate the end sequence wall (only after chase starts)
        if (endSequenceWall != null)
        {
            endSequenceWall.SetActive(true);
            if (debugLogs) Debug.Log("[FinalSequence] End sequence wall activated");
        }

        // Keep chase wall active - don't disable it
        // (Player may need to move back through it)
    }

    /// <summary>
    /// Trigger Walk animation with a small delay to allow Spawn to complete
    /// </summary>
    private IEnumerator TriggerWalkAnimationDelayed()
    {
        // Wait a short time to let Spawn animation start/finish
        yield return new WaitForSeconds(0.2f);

        Animator animator = GetPainterAnimator();
        if (animator != null)
        {
            // Reset any states to ensure clean transition
            animator.ResetTrigger("Spawn");
            
            // Now trigger Walk
            animator.SetTrigger("Walk");
            if (debugLogs) Debug.Log("[FinalSequence] Triggered 'Walk' animation on Painter (delayed)");
        }
        else
        {
            Debug.LogWarning("[FinalSequence] No Animator found to trigger Walk animation!");
        }
    }

    /// <summary>
    /// Called when painter catches the player
    /// </summary>
    public void OnPlayerCaught()
    {
        if (debugLogs) Debug.Log("[FinalSequence] PLAYER CAUGHT!");

        // Trigger designer event
        onPlayerCaught?.Invoke();

        // Show game over screen
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(true);
            Time.timeScale = 0f; // Pause game
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// Called when player reaches the end sequence trigger
    /// </summary>
    public void StartEndSequence()
    {
        if (debugLogs) Debug.Log("[FinalSequence] END SEQUENCE STARTED!");

        // Stop the chase and despawn painter
        StopChaseAndDespawnPainter();

        // Complete the finale task
        if (finaleTask != null)
        {
            finaleTask.CompleteFinale();
            if (taskManager != null)
            {
                taskManager.completedTasks.Add("FinaleTask");
                taskManager.RemoveActiveTask(finaleTask);
            }
        }

        // Trigger designer event
        onEndSequenceStart?.Invoke();

        // Disable end sequence wall after triggering
        if (endSequenceWall != null)
            endSequenceWall.SetActive(false);

        // You can add a coroutine here for multi-step end sequences
        StartCoroutine(EndSequenceCoroutine());
    }

    /// <summary>
    /// Stop the chase and remove the painter from the scene
    /// </summary>
    private void StopChaseAndDespawnPainter()
    {
        if (painterInstance != null)
        {
            // Deactivate chase if active
            var painterAI = painterInstance.GetComponent<PainterAI>();
            if (painterAI != null)
            {
                painterAI.SetChaseActive(false);
            }

            // Destroy the painter
            Destroy(painterInstance);
            painterInstance = null;
            
            if (debugLogs) Debug.Log("[FinalSequence] Painter despawned");
        }

        // Mark chase as inactive
        chaseActive = false;
    }

    /// <summary>
    /// Optional coroutine for multi-step end sequence
    /// </summary>
    private IEnumerator EndSequenceCoroutine()
    {
        // Wait for designer events to complete
        yield return new WaitForSeconds(0.5f);

        // Trigger completion event
        onEndSequenceComplete?.Invoke();

        if (debugLogs) Debug.Log("[FinalSequence] End sequence complete!");
    }

    /// <summary>
    /// Reset the finale sequence to initial state (called on scene start or game restart)
    /// </summary>
    public void ResetSequence()
    {
        if (debugLogs) Debug.Log("[FinalSequence] Resetting finale sequence to initial state...");

        // Reset state flags
        sequenceActivated = false;
        chaseActive = false;
        sequenceReady = false;
        lastCompletedCount = -1;
        cutscenePlaying = false;

        // Unfreeze player if frozen
        if (cachedPlayerManager != null)
        {
            FreezePlayer(false);
            cachedPlayerManager = null;
        }

        // Destroy painter instance if it exists
        if (painterInstance != null)
        {
            Destroy(painterInstance);
            painterInstance = null;
            if (debugLogs) Debug.Log("[FinalSequence] Destroyed existing painter instance");
        }

        // Remove finale task from TaskManager if it exists
        if (finaleTask != null && taskManager != null)
        {
            if (taskManager.tasks.Contains(finaleTask))
            {
                taskManager.tasks.Remove(finaleTask);
            }
            taskManager.RemoveActiveTask(finaleTask);
            finaleTask = null;
        }

        // Deactivate all trigger walls
        if (chaseWall != null) chaseWall.SetActive(false);
        if (endSequenceWall != null) endSequenceWall.SetActive(false);
        if (activationTrigger != null) activationTrigger.SetActive(false);

        // Deactivate all spawn objects
        DeactivateAllSpawnObjects();

        // Hide game over screen if visible
        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(false);
        }

        // Reset time scale in case it was paused
        Time.timeScale = 1f;

        if (debugLogs) Debug.Log("[FinalSequence] Finale sequence reset complete");
    }

    /// <summary>
    /// Called when game is restarted - ensures clean state
    /// </summary>
    private void OnDestroy()
    {
        // Clean up painter instance when this object is destroyed
        if (painterInstance != null)
        {
            Destroy(painterInstance);
            painterInstance = null;
        }
    }

    /// <summary>
    /// Manual trigger for debugging (call from inspector context menu)
    /// </summary>
    [ContextMenu("DEBUG: Force Activate Sequence")]
    public void DebugForceActivate()
    {
        sequenceReady = true;
        ActivateFinalSequence();
    }

    [ContextMenu("DEBUG: Force Start Chase")]
    public void DebugForceChase()
    {
        StartChase();
    }

    [ContextMenu("DEBUG: Force End Sequence")]
    public void DebugForceEndSequence()
    {
        StartEndSequence();
    }

    [ContextMenu("DEBUG: Reset Sequence")]
    public void DebugResetSequence()
    {
        ResetSequence();
    }

    /// <summary>
    /// Public method to be called when game restarts from pause menu or main menu
    /// </summary>
    public static void ResetAllSequences()
    {
        var sequenceManager = FindAnyObjectByType<FinalSequenceManager>();
        if (sequenceManager != null)
        {
            sequenceManager.ResetSequence();
        }
    }
}

/// <summary>
/// Simple component to handle trigger events for wall objects
/// </summary>
public class WallTriggerHandler : MonoBehaviour
{
    private System.Action<Collider> onTriggerCallback;

    public void Initialize(System.Action<Collider> callback)
    {
        onTriggerCallback = callback;
    }

    private void OnTriggerEnter(Collider other)
    {
        onTriggerCallback?.Invoke(other);
    }
}
