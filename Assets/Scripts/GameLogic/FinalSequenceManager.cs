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

    [Header("Cutscene Settings")]
    [Tooltip("Transform that the camera will look at during the cutscene")]
    [SerializeField] private Transform cutsceneCameraTarget;
    [Tooltip("(Optional) GameObject to disable after spawn cutscene - auto-searches for 'Ceiling hand animation' child in painter prefab")]
    [SerializeField] private GameObject ceilingHandObject;
    
    // Runtime reference to the ceiling hand found in the painter instance
    private GameObject runtimeCeilingHandObject;

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
    [Tooltip("Death screen to show when caught by painter (shown while game is paused)")]
    [SerializeField] private GameObject painterDeathScreen;
    [Tooltip("Scene to load after end sequence (usually scene index 2)")]
    [SerializeField] private int endSequenceSceneIndex = 2;
    [Tooltip("Transform where the player will respawn after being caught")]
    [SerializeField] private Transform playerRespawnPosition;

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
            return false;

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
            return;

        Vector3 spawnPos = painterSpawnPoint != null ? painterSpawnPoint.position : transform.position;
        Quaternion spawnRot = painterSpawnPoint != null ? painterSpawnPoint.rotation : Quaternion.identity;

        painterInstance = Instantiate(painterPrefab, spawnPos, spawnRot);
        painterInstance.name = "Painter_AI";

        // Find the ceiling hand object in the painter instance
        FindCeilingHandInPainter();

        // Painter should start inactive/idle until chase triggers
        var painterAI = painterInstance.GetComponent<PainterAI>();
        if (painterAI != null)
        {
            painterAI.SetChaseActive(false);
        }

        // Disable the painter GameObject until chase trigger is entered
        // This prevents it from being visible before the cutscene
        painterInstance.SetActive(false);
        if (debugLogs) Debug.Log($"[FinalSequence] Painter spawned at {spawnPos} and set to inactive (will activate on chase trigger)");
    }

    /// <summary>
    /// Find the ceiling hand object in the painter instance (child named "Ceiling hand animation")
    /// </summary>
    private void FindCeilingHandInPainter()
    {
        if (painterInstance == null) return;

        // The ceiling hand is instantiated as part of the prefab, so we need to search in the painter's children
        // Use assigned ceiling hand if provided, otherwise search for it
        if (ceilingHandObject != null)
        {
            // If manually assigned, find it in the instantiated painter by name
            foreach (Transform child in painterInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == ceilingHandObject.name)
                {
                    runtimeCeilingHandObject = child.gameObject;
                    if (debugLogs) Debug.Log($"[FinalSequence] Found assigned ceiling hand object: {runtimeCeilingHandObject.name}");
                    break;
                }
            }
        }
        else
        {
            // Auto-search for "Ceiling hand animation" in all children (including inactive ones)
            foreach (Transform child in painterInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Ceiling hand animation")
                {
                    runtimeCeilingHandObject = child.gameObject;
                    if (debugLogs) Debug.Log($"[FinalSequence] Auto-found ceiling hand object: {runtimeCeilingHandObject.name} at path: {GetGameObjectPath(child.gameObject)}");
                    break;
                }
            }
        }

        if (runtimeCeilingHandObject == null)
        {
            if (debugLogs)
            {
                Debug.Log("[FinalSequence] Could not find 'Ceiling hand animation' in painter prefab");
                Debug.Log("[FinalSequence] Listing all children of painter instance:");
                foreach (Transform child in painterInstance.GetComponentsInChildren<Transform>(true))
                {
                    Debug.Log($"  - {GetGameObjectPath(child.gameObject)}");
                }
            }
        }
    }

    /// <summary>
    /// Helper method to get the full path of a GameObject in the hierarchy
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null && current != painterInstance.transform)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
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

        // Store original camera look direction for restoration
        Transform cameraTransform = null;
        Quaternion originalCameraRotation = Quaternion.identity;
        
        if (cachedPlayerManager != null && cachedPlayerManager.playerCamera != null)
        {
            cameraTransform = cachedPlayerManager.playerCamera.transform;
            originalCameraRotation = cameraTransform.rotation;
        }

        // Freeze player
        FreezePlayer(true);

        // Activate the painter GameObject (it was inactive since spawn)
        if (painterInstance != null)
        {
            painterInstance.SetActive(true);
            if (debugLogs) Debug.Log($"[FinalSequence] Painter GameObject activated for cutscene (Active: {painterInstance.activeSelf})");
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
                
                // Make camera look at target during cutscene
                float cutsceneDuration = (float)playableDirector.duration;
                float elapsed = 0f;
                
                while (playableDirector.state == UnityEngine.Playables.PlayState.Playing)
                {
                    // Smoothly rotate camera to look at target
                    if (cameraTransform != null && cutsceneCameraTarget != null)
                    {
                        Vector3 directionToTarget = cutsceneCameraTarget.position - cameraTransform.position;
                        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * 2f);
                    }
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                if (debugLogs) Debug.Log("[FinalSequence] Cutscene Timeline finished");
            }
            else
            {
                // Still wait a bit so it doesn't feel instant
                yield return new WaitForSeconds(1f);
            }
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        // Disable the ceiling hand object after cutscene ends
        if (runtimeCeilingHandObject != null)
        {
            runtimeCeilingHandObject.SetActive(false);
            if (debugLogs) Debug.Log("[FinalSequence] Ceiling hand object disabled after cutscene");
        }

        // CRITICAL: Enable NavMeshAgent NOW before unfreezing player and starting chase
        // The cutscene is over, so it's safe to enable pathfinding
        if (painterInstance != null)
        {
            var navAgent = painterInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = true;
                if (debugLogs) Debug.Log($"[FinalSequence] NavMeshAgent enabled after cutscene - Enabled: {navAgent.enabled}");
            }
        }

        // Unfreeze player
        FreezePlayer(false);

        cutscenePlaying = false;
        if (debugLogs) Debug.Log("[FinalSequence] CUTSCENE ENDED - Starting chase");

        // Now start the actual chase (this will trigger the Walk animation)
        StartChase();
    }

    /// <summary>
    /// Freeze or unfreeze the player (movement, camera, interactions)
    /// </summary>
    private void FreezePlayer(bool freeze)
    {
        if (cachedPlayerManager == null)
            return;

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

        // Trigger designer event
        onChaseStart?.Invoke();

        // Get the Painter Animated child and reset its transform
        if (painterInstance != null)
        {
            Transform painterAnimated = painterInstance.transform.Find("Painter Animated");
            if (painterAnimated != null)
            {
                // Immediately snap position and rotation (no interpolation for instant response)
                painterAnimated.localPosition = new Vector3(0f, -0.95f, 0f);
                painterAnimated.localRotation = Quaternion.identity;
                painterAnimated.localScale = Vector3.one;
                if (debugLogs) Debug.Log("[FinalSequence] Reset 'Painter Animated' transform - Pos:(0,-0.95,0), Rot:(0,0,0), Scale:(1,1,1)");
                
                // Find and reset Paint Splotch 1 position to avoid bugs
                Transform paintSplotch = painterAnimated.Find("Paint Splotch 1");
                if (paintSplotch != null)
                {
                    paintSplotch.localPosition = Vector3.zero;
                    paintSplotch.localRotation = Quaternion.identity;
                    if (debugLogs) Debug.Log("[FinalSequence] Reset 'Paint Splotch 1' transform to (0,0,0)");
                }
            }
        }

        // Immediately trigger Walk animation on painter
        Animator animator = GetPainterAnimator();
        if (animator != null)
        {
            // Make sure to reset any previous triggers
            animator.ResetTrigger("Spawn");
            
            // Set Walk trigger
            animator.SetTrigger("Walk");
            if (debugLogs) Debug.Log("[FinalSequence] Triggered 'Walk' animation on Painter");
        }

        // Activate painter AI chase
        if (painterInstance != null)
        {
            var painterAI = painterInstance.GetComponent<PainterAI>();
            
            if (painterAI != null)
            {
                // NavMeshAgent is already enabled at the end of cutscene
                // Just activate the chase
                painterAI.SetChaseActive(true);
                
                if (debugLogs) Debug.Log("[FinalSequence] Chase activated on PainterAI");
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
    /// Called when painter catches the player
    /// </summary>
    public void OnPlayerCaught()
    {
        if (debugLogs) Debug.Log("[FinalSequence] PLAYER CAUGHT!");

        // Trigger designer event
        onPlayerCaught?.Invoke();

        // Start the caught sequence (fade, respawn, reset)
        StartCoroutine(PlayerCaughtSequence());
    }

    /// <summary>
    /// Coroutine that handles player caught sequence: fade, despawn painter, respawn player, reset chase
    /// </summary>
    private IEnumerator PlayerCaughtSequence()
    {
        if (debugLogs) Debug.Log("[FinalSequence] Starting player caught sequence");

        // Get player reference if we don't have it cached
        if (cachedPlayerManager == null)
        {
            cachedPlayerManager = FindAnyObjectByType<PlayerManager>();
        }

        // Freeze player during transition
        if (cachedPlayerManager != null)
        {
            cachedPlayerManager.enabled = false;
            if (debugLogs) Debug.Log("[FinalSequence] Player frozen for respawn");
        }

        // Request fade to black
        if (ScreenFadeManager.Instance != null)
        {
            ScreenFadeManager.Instance.FadeToBlackAndLoadScene(-1); // -1 means no scene load, just fade
        }

        // Wait for fade to complete (match fade duration)
        yield return new WaitForSeconds(2.5f); // Fade duration + hold duration

        // Pause game and show death screen
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (painterDeathScreen != null)
        {
            painterDeathScreen.SetActive(true);
            if (debugLogs) Debug.Log("[FinalSequence] Painter death screen shown - waiting for player input");
        }
        else
        {
            // If no death screen assigned, auto-restart after a brief pause
            yield return new WaitForSecondsRealtime(2f);
            RestartFromCheckpoint();
        }

        // Note: The sequence continues when RestartFromCheckpoint() is called by a button
    }

    /// <summary>
    /// Restart from checkpoint after being caught by painter
    /// Call this from the death screen "Restart" button
    /// </summary>
    public void RestartFromCheckpoint()
    {
        if (debugLogs) Debug.Log("[FinalSequence] Restarting from checkpoint");

        // Hide death screen
        if (painterDeathScreen != null)
        {
            painterDeathScreen.SetActive(false);
        }

        // Resume game time
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Start the respawn sequence
        StartCoroutine(RespawnSequence());
    }

    /// <summary>
    /// Coroutine that handles respawn after checkpoint restart
    /// </summary>
    private IEnumerator RespawnSequence()
    {
        if (debugLogs) Debug.Log("[FinalSequence] Starting respawn sequence");

        // Destroy the old painter completely
        if (painterInstance != null)
        {
            Destroy(painterInstance);
            painterInstance = null;
            if (debugLogs) Debug.Log("[FinalSequence] Old painter destroyed");
        }

        // Reset chase state - IMPORTANT: don't start chase yet!
        chaseActive = false;
        cutscenePlaying = false;
        if (debugLogs) Debug.Log("[FinalSequence] Chase state reset");

        // Respawn player at designated position
        if (cachedPlayerManager != null && playerRespawnPosition != null)
        {
            // Disable character controller to teleport
            var characterController = cachedPlayerManager.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            // Move player to respawn position
            cachedPlayerManager.transform.position = playerRespawnPosition.position;
            cachedPlayerManager.transform.rotation = playerRespawnPosition.rotation;

            if (debugLogs) Debug.Log($"[FinalSequence] Player respawned at {playerRespawnPosition.name}");

            // Re-enable character controller
            if (characterController != null)
            {
                characterController.enabled = true;
            }

            // Re-enable player manager
            cachedPlayerManager.enabled = true;
        }
        else
        {
            if (playerRespawnPosition == null && debugLogs)
                Debug.Log("[FinalSequence] Player respawn position not assigned");
        }

        // Gradual fade back in (clear the screen)
        if (ScreenFadeManager.Instance != null)
        {
            ScreenFadeManager.Instance.FadeFromBlack();
        }

        // Orient player camera to look at cutscene target before fade completes
        if (cachedPlayerManager != null && cachedPlayerManager.playerCamera != null && cutsceneCameraTarget != null)
        {
            Transform cameraTransform = cachedPlayerManager.playerCamera.transform;
            Vector3 directionToTarget = cutsceneCameraTarget.position - cameraTransform.position;
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            cameraTransform.rotation = targetRotation;
            
            if (debugLogs) Debug.Log($"[FinalSequence] Player camera oriented to look at cutscene target");
        }

        // Wait for fade to complete
        yield return new WaitForSeconds(2f);

        // Spawn a NEW painter (fresh instance that will pathfind properly)
        if (painterPrefab == null)
        {
            if (debugLogs) Debug.Log("[FinalSequence] Painter prefab not assigned");
            yield break;
        }

        Vector3 spawnPos = painterSpawnPoint != null ? painterSpawnPoint.position : transform.position;
        Quaternion spawnRot = painterSpawnPoint != null ? painterSpawnPoint.rotation : Quaternion.identity;

        painterInstance = Instantiate(painterPrefab, spawnPos, spawnRot);
        painterInstance.name = "Painter_AI";
        
        if (debugLogs) Debug.Log($"[FinalSequence] New painter spawned at {spawnPos}");

        // Find the ceiling hand object in the new painter instance
        FindCeilingHandInPainter();

        // Set painter INACTIVE so it's hidden until cutscene starts
        painterInstance.SetActive(false);

        // Start cutscene immediately (no wait needed since player is already at checkpoint)
        if (debugLogs) Debug.Log("[FinalSequence] Starting cutscene immediately after respawn");
        StartCoroutine(CutsceneCoroutine());
    }

    /// <summary>
    /// Called when player reaches the end sequence trigger
    /// </summary>
    public void StartEndSequence()
    {
        if (debugLogs) Debug.Log("[FinalSequence] END SEQUENCE STARTED!");

        // Mark game as completed in PlayerPrefs (unlocks day mode)
        PlayerPrefs.SetInt("GameCompleted", 1);
        PlayerPrefs.Save();
        if (debugLogs) Debug.Log("[FinalSequence] Game completion saved to PlayerPrefs - Day Mode unlocked!");

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

        // Start the end sequence coroutine (fade to black and load scene)
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

        if (debugLogs) Debug.Log("[FinalSequence] End sequence complete - Fading to black and loading scene");

        // Fade to black and load the next scene (scene 2)
        ScreenFadeManager.FadeAndLoadScene(endSequenceSceneIndex);
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

        // Clear runtime ceiling hand reference (it will be re-found when painter spawns next time)
        runtimeCeilingHandObject = null;

        // Re-enable ceiling hand object for next sequence (if manually assigned)
        if (ceilingHandObject != null)
        {
            ceilingHandObject.SetActive(true);
            if (debugLogs) Debug.Log("[FinalSequence] Ceiling hand object re-enabled for next sequence");
        }

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
