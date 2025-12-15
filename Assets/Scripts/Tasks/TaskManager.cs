using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TaskManager : MonoBehaviour
{
    [Header("Player reference")]
    [Tooltip("Optional: assign player transform, otherwise will search by tag 'Player' or PlayerStats.Instance.")]
    public Transform playerTransform;

    [Header("Player Manager (for input)")]
    [Tooltip("Optional: assign PlayerManager to use the project's Input Action 'Interact'.")]
    public PlayerManager playerManager;

    [Header("Painting Task Settings")]
    [Tooltip("Painting Transforms available for the painting task.")]
    public List<Transform> paintingTargets = new List<Transform>();
    [Tooltip("Player must be at least this far for a painting to fall.")]
    public float paintingRequiredPlayerDistanceToFall = 8f;
    [Tooltip("Interaction distance to return painting.")]
    public float paintingInteractionDistance = 2.5f;
    [Tooltip("Minimum number of paintings that may fall when the painting task activates.")]
    public int paintingMinFallCount = 1;
    [Tooltip("Maximum number of paintings that may fall when the painting task activates.")]
    public int paintingMaxFallCount = 3;
    [Tooltip("If true, force ALL configured paintingTargets to fall when the painting task activates.")]
    public bool paintingForceAllToFall = false;
    [Header("Per-task timing & repeat (Painting)")]
    [Tooltip("If true the Painting task may repeat after completion. If false it will only run once.")]
    public bool paintingRepeat = false;
    [Tooltip("Minimum randomized time before the Painting task may start (not used - painting runs at Start).")]
    public float paintingMinTimeToStart = 30f;
    [Tooltip("Maximum randomized time before the Painting task may start (not used - painting runs at Start).")]
    public float paintingMaxTimeToStart = 90f;

    [Header("Trash Task Settings")]
    [Tooltip("Trash prefab to spawn.")]
    public GameObject trashPrefab;
    [Tooltip("Legacy single list of trash spawn locations (kept for backwards compatibility).")]
    public Transform[] trashSpawnPoints;
    [Tooltip("Trash spawn GROUP A: if selected at activation, trash will be spawned at ALL transforms in this group.")]
    public Transform[] trashSpawnGroupA;
    [Tooltip("Trash spawn GROUP B: if selected at activation, trash will be spawned at ALL transforms in this group.")]
    public Transform[] trashSpawnGroupB;
    [Tooltip("Trash spawn GROUP C: if selected at activation, trash will be spawned at ALL transforms in this group.")]
    public Transform[] trashSpawnGroupC;
    [Header("Per-task timing & repeat")]
    [Tooltip("If true the Trash task may repeat after completion. If false it will only run once.")]
    public bool trashRepeat = false;
    [Tooltip("Minimum randomized time before the Trash task may start (used when scheduling).")]
    public float trashMinTimeToStart = 30f;
    [Tooltip("Maximum randomized time before the Trash task may start (used when scheduling).")]
    public float trashMaxTimeToStart = 90f;
    [Tooltip("Distance player must be to interact with trash.")]
    public float trashInteractionDistance = 2.5f;

    [Header("Task names")]
    [Tooltip("Human readable name used for the Painting task (logged/completed list).")]
    public string PaintingTaskName = "PaintingTask";
    [Tooltip("Human readable name used for the Trash task (logged/completed list).")]
    public string TrashTaskName = "TrashTask";

    // runtime
    public List<ITask> tasks = new();
    // support multiple concurrent active tasks
    private List<ITask> activeTasks = new();
    private float nextTaskTimer = 0f;

    // runtime direct refs for clarity
    private ITask paintingTaskRef;
    private ITask trashTaskRef;

    // simple tracking
    public List<string> completedTasks = new List<string>();

    // store indices that should no longer be considered (non-repeat tasks that have completed)
    private System.Collections.Generic.HashSet<int> disabledTaskIndices;

    private void OnEnable()
    {
        // intentionally silent to avoid console spam
    }

    private void OnDisable()
    {
        // intentionally silent to avoid console spam
    }

    private void Awake()
    {
        Debug.Log("[TaskManager] Awake");

        if (playerManager == null)
            playerManager = Object.FindFirstObjectByType<PlayerManager>();

        // ensure playerTransform follows PlayerManager if available
        if (playerManager != null && playerTransform == null)
            playerTransform = playerManager.transform;
        // find player if not assigned
        if (playerTransform == null)
        {
            if (PlayerStats.Instance != null)
                playerTransform = PlayerStats.Instance.transform;
            else
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
        }

        // ensure task name defaults (allows inspector override; guarantees non-empty names)
        if (string.IsNullOrWhiteSpace(PaintingTaskName)) PaintingTaskName = "PaintingTask";
        if (string.IsNullOrWhiteSpace(TrashTaskName)) TrashTaskName = "TrashTask";

        // create task instances and initialize them
        var paintingTask = new PaintingTask();
        paintingTask.Initialize(this);

        var trashTask = new TrashTask();
        trashTask.Initialize(this);

        tasks.Add(paintingTask);
        tasks.Add(trashTask);

        // keep direct refs (index 0 = painting, index 1 = trash based on creation order)
        paintingTaskRef = paintingTask;
        trashTaskRef = trashTask;

        // track non-repeatable tasks by index once they finish (empty initially)
        disabledTaskIndices = new System.Collections.Generic.HashSet<int>();

        // quick sanity checks to help debugging
        if (trashPrefab == null)
            Debug.LogWarning("[TaskManager] trashPrefab is not assigned in inspector - trash cannot spawn.");
        if (!HasNonNull(trashSpawnGroupA) && !HasNonNull(trashSpawnGroupB) && !HasNonNull(trashSpawnGroupC) && !HasNonNull(trashSpawnPoints))
            Debug.LogWarning("[TaskManager] No trash spawn points/groups assigned. Assign inspector entries or use legacy trashSpawnPoints.");

        // schedule trash timer by default; painting will be attempted at Start
        ScheduleNextTask();
    }

    private void Start()
    {
        Debug.Log("[TaskManager] Start");

        // Re-resolve player references in Start in case PlayerStats/PlayerManager weren't initialized when Awake ran
        if (playerManager == null)
            playerManager = Object.FindFirstObjectByType<PlayerManager>();

        if (playerManager != null && playerTransform == null)
            playerTransform = playerManager.transform;

        if (playerTransform == null)
        {
            if (PlayerStats.Instance != null)
                playerTransform = PlayerStats.Instance.transform;
            else
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }
        }

        // Try to run painting at beginning of the night deterministically.
        // Only start if not disabled and the task reports it can activate.
        if (paintingTaskRef != null)
        {
            int paintIdx = tasks.IndexOf(paintingTaskRef);
            if (paintIdx >= 0 && (disabledTaskIndices == null || !disabledTaskIndices.Contains(paintIdx)))
            {
                Debug.Log("[TaskManager] Attempting to activate PaintingTask at Start");
                if (paintingTaskRef.CanActivate(playerTransform))
                {
                    Debug.Log("[TaskManager] PaintingTask.CanActivate returned true -> Activating");
                    if (!activeTasks.Contains(paintingTaskRef))
                    {
                        activeTasks.Add(paintingTaskRef);
                        paintingTaskRef.Activate(playerTransform);
                    }
                }
                else
                {
                    Debug.Log("[TaskManager] PaintingTask.CanActivate returned false at Start");
                }
            }
        }
    }

    private void ScheduleNextTask()
    {
        // If trash disabled, no timer is set.
        if (disabledTaskIndices != null && disabledTaskIndices.Contains(tasks.IndexOf(trashTaskRef)))
        {
            nextTaskTimer = 0f;
            Debug.Log("[TaskManager] Trash task disabled — not scheduling timer");
            return;
        }

        // Use trash task's configured min/max range to schedule next
        float minT = Mathf.Max(0f, trashMinTimeToStart);
        float maxT = Mathf.Max(minT, trashMaxTimeToStart);

        nextTaskTimer = Random.Range(minT, maxT);
        Debug.Log($"[TaskManager] Scheduled next trash timer: {nextTaskTimer:0.00}s (range {minT}-{maxT})");
    }

    private void Update()
    {
        // Tick active tasks concurrently
        if (activeTasks != null && activeTasks.Count > 0)
        {
            for (int i = activeTasks.Count - 1; i >= 0; i--)
            {
                var t = activeTasks[i];
                if (t == null)
                {
                    activeTasks.RemoveAt(i);
                    continue;
                }

                t.Tick(playerTransform);

                if (t.IsCompleted)
                {
                    completedTasks.Add(t.TaskName);
                    var checker = FindAnyObjectByType<TaskChecker>();
                    if (checker != null) checker.CheckCompletedTasks();

                    // mark non-repeatable tasks as disabled based on their concrete type
                    int idx = tasks.IndexOf(t);
                    if (idx >= 0)
                    {
                        if (t is PaintingTask && !paintingRepeat)
                        {
                            disabledTaskIndices.Add(idx);
                            Debug.Log($"[TaskManager] Marked PaintingTask index {idx} disabled (non-repeatable)");
                        }
                        if (t is TrashTask && !trashRepeat)
                        {
                            disabledTaskIndices.Add(idx);
                            Debug.Log($"[TaskManager] Marked TrashTask index {idx} disabled (non-repeatable)");
                        }
                    }

                    t.Deactivate();
                    activeTasks.RemoveAt(i);
                }
            }
        }

        // Always tick the trash timer so it can run regardless of other tasks
        if (nextTaskTimer > 0f)
        {
            nextTaskTimer -= Time.deltaTime;
            if (nextTaskTimer <= 0f)
            {
                nextTaskTimer = 0f;
                Debug.Log("[TaskManager] Trash timer reached zero");
            }
        }

        // If timer expired, attempt to start trash (concurrent with other tasks allowed)
        if (nextTaskTimer <= 0f)
        {
            // Attempt to start trash now. If activated, schedule the next timer immediately (cooldown on spawn).
            if (TryStartTrashFromTimer())
            {
                // schedule next attempt (either cooldown or next window)
                ScheduleNextTask();
            }
            else
            {
                // nothing started - schedule a retry
                ScheduleNextTask();
            }
        }
    }

    // Helper extracted from previous code: tries to start trash using available groups or legacy points.
    // Returns true if the trash task was activated.
    private bool TryStartTrashFromTimer()
    {
        Debug.Log("[TaskManager] TryStartTrashFromTimer invoked");

        int trashIdx = tasks.IndexOf(trashTaskRef);
        if (trashTaskRef == null || trashIdx < 0)
        {
            Debug.Log("[TaskManager] No trash task reference available");
            return false;
        }

        if (disabledTaskIndices != null && disabledTaskIndices.Contains(trashIdx))
        {
            Debug.Log("[TaskManager] Trash task is disabled (non-repeatable) - won't start");
            return false;
        }

        // Build list of candidate groups that actually contain at least one non-null transform
        var groups = new List<Transform[]>();
        if (HasNonNull(trashSpawnGroupA)) groups.Add(trashSpawnGroupA);
        if (HasNonNull(trashSpawnGroupB)) groups.Add(trashSpawnGroupB);
        if (HasNonNull(trashSpawnGroupC)) groups.Add(trashSpawnGroupC);

        Transform[] chosenGroup = null;
        if (groups.Count > 0)
        {
            chosenGroup = groups[Random.Range(0, groups.Count)];
            Debug.Log("[TaskManager] Chosen trash group from A/B/C");
            Debug.Log(DescribeGroup(chosenGroup));
        }
        else if (trashSpawnPoints != null && trashSpawnPoints.Length > 0)
        {
            if (HasNonNull(trashSpawnPoints))
            {
                chosenGroup = trashSpawnPoints;
                Debug.Log("[TaskManager] Using legacy trashSpawnPoints as chosen group");
                Debug.Log(DescribeGroup(chosenGroup));
            }
        }

        if (chosenGroup == null || chosenGroup.Length == 0)
        {
            Debug.Log("[TaskManager] No valid trash spawn points found; cannot start trash now");
            return false;
        }

        if (trashTaskRef is TrashTask tt)
        {
            tt.SetPlannedGroup(chosenGroup);
            Debug.Log("[TaskManager] PlannedGroup set on TrashTask");
        }

        // Verify the task can activate (plannedGroup will be considered) and activate it
        if (trashTaskRef.CanActivate(playerTransform))
        {
            Debug.Log("[TaskManager] TrashTask.CanActivate returned true -> Activating TrashTask (concurrent allowed)");
            // ensure we don't add the same task multiple times
            if (!activeTasks.Contains(trashTaskRef))
            {
                activeTasks.Add(trashTaskRef);
                trashTaskRef.Activate(playerTransform);
            }
            return true;
        }
        else
        {
            Debug.Log("[TaskManager] TrashTask.CanActivate returned false after planning group");
            if (trashTaskRef is TrashTask tt2) tt2.SetPlannedGroup(null);
            return false;
        }
    }

    // Helper to ensure a Transform[] contains at least one non-null transform
    private bool HasNonNull(Transform[] arr)
    {
        if (arr == null || arr.Length == 0) return false;
        foreach (var t in arr) if (t != null) return true;
        return false;
    }

    // Helper to produce a readable description of a chosen group for logging
    private string DescribeGroup(Transform[] grp)
    {
        if (grp == null) return "[Group=null]";
        var names = new List<string>();
        for (int i = 0; i < grp.Length; i++)
        {
            var t = grp[i];
            names.Add(t != null ? $"#{i}:{t.name}" : $"#{i}:null");
        }
        return $"[Group entries={grp.Length}] " + string.Join(", ", names.ToArray());
    }

    // Public debug methods to force tasks immediately (useful while iterating)
    [ContextMenu("Trigger Trash Now")]
    public void DebugTriggerTrashNow()
    {
        Debug.Log("[TaskManager] DebugTriggerTrashNow called");
        nextTaskTimer = 0f;
    }

    [ContextMenu("Trigger Painting Now")]
    public void DebugTriggerPaintingNow()
    {
        Debug.Log("[TaskManager] DebugTriggerPaintingNow called");
        if (paintingTaskRef != null && paintingTaskRef.CanActivate(playerTransform))
        {
            if (!activeTasks.Contains(paintingTaskRef))
            {
                activeTasks.Add(paintingTaskRef);
                paintingTaskRef.Activate(playerTransform);
            }
        }
        else Debug.Log("[TaskManager] PaintingTask cannot activate now");
    }

    public bool InteractPressed()
    {
        if (playerManager != null && playerManager.inputActions != null)
        {
            return playerManager.inputActions.Player.Interact.triggered;
        }

        // Fallback to keyboard 'E' using the new Input System (rare fallback)
        var kb = Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
    }
}