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

    [Header("Special single-painting mini-task")]
    [Tooltip("Assign the specific painting Transform that will be used for the single static fall mini-task.")]
    public Transform specialPaintingTarget;
    [Tooltip("Seconds until the phone 'allows' the painting to fall.")]
    public float specialPhoneDelay = 10f;
    [Tooltip("Optional AudioSource used to play completion audio for the mini-task.")]
    public AudioSource specialPhoneAudioSource;
    [Tooltip("Optional AudioClip played by specialPhoneAudioSource when the mini-task completes.")]
    public AudioClip specialPhoneCompleteClip;

    // New: explicit CamImage reference for the special camera to check zoom state.
    // Assign the CamImage (B2) here in the inspector to avoid scene searches.
    [Tooltip("Optional: assign the CamImage (e.g. B2) to be used by the single painting fall task. If null the task will search by group/name.")]
    public CamImage specialCamImage;

    // New: whether the phone call flow is used (if true the task will wait for manager.specialPhoneCallStarted before starting phone delay)
    [Tooltip("If true the special phone-delay will only start once specialPhoneCallStarted is set to true. Leave false to start the delay immediately.")]
    public bool specialPhoneUseCall = false;

    // New: placeholder boolean indicating an external phone call has started.
    // You can set this to true from other systems when you implement the phone call.
    [HideInInspector] public bool specialPhoneCallStarted = false;

    // runtime
    public List<ITask> tasks = new();
    // support multiple concurrent active tasks
    private List<ITask> activeTasks = new();
    private float nextTaskTimer = 0f;

    // runtime direct refs for clarity
    private ITask paintingTaskRef;
    private ITask trashTaskRef;
    private ITask singlePaintingTaskRef; // <- new reference

    // simple tracking
    public List<string> completedTasks = new List<string>();

    // store indices that should no longer be considered (non-repeat tasks that have completed)
    private System.Collections.Generic.HashSet<int> disabledTaskIndices;

    // Expose last chosen trash group name so designer scripts can read it
    public string LastTrashGroupName { get; private set; }

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

        // New: create and register the single-painting mini-task (keeps existing code intact)
        var singlePaintingTask = new SinglePaintingFallTask();
        singlePaintingTask.Initialize(this);

        // register tasks
        tasks.Add(singlePaintingTask);
        tasks.Add(paintingTask);
        tasks.Add(trashTask);

        // keep direct refs (so we can reference them at Start)
        singlePaintingTaskRef = singlePaintingTask;
        paintingTaskRef = paintingTask;
        trashTaskRef = trashTask;

        // track non-repeatable tasks by index once they finish (empty initially)
        disabledTaskIndices = new System.Collections.Generic.HashSet<int>();

        // quick sanity checks (no debug logs to avoid flooding)
        if (trashPrefab == null)
        {
            // intentionally silent
        }
        if (!HasNonNull(trashSpawnGroupA) && !HasNonNull(trashSpawnGroupB) && !HasNonNull(trashSpawnGroupC) && !HasNonNull(trashSpawnPoints))
        {
            // intentionally silent
        }

        // schedule trash timer by default; painting will be attempted at Start
        ScheduleNextTask();
    }

    private void Start()
    {
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

        // Try to run single mini-task at Start (deterministic first-night event).
        if (singlePaintingTaskRef != null)
        {
            int singleIdx = tasks.IndexOf(singlePaintingTaskRef);
            if (singleIdx >= 0 && (disabledTaskIndices == null || !disabledTaskIndices.Contains(singleIdx)))
            {
                if (singlePaintingTaskRef.CanActivate(playerTransform))
                {
                    if (!activeTasks.Contains(singlePaintingTaskRef))
                    {
                        activeTasks.Add(singlePaintingTaskRef);
                        singlePaintingTaskRef.Activate(playerTransform);
                    }
                }
            }
        }

        // Try to run painting at beginning of the night deterministically.
        // Only start if not disabled and the task reports it can activate.
        if (paintingTaskRef != null)
        {
            int paintIdx = tasks.IndexOf(paintingTaskRef);
            if (paintIdx >= 0 && (disabledTaskIndices == null || !disabledTaskIndices.Contains(paintIdx)))
            {
                if (paintingTaskRef.CanActivate(playerTransform))
                {
                    if (!activeTasks.Contains(paintingTaskRef))
                    {
                        activeTasks.Add(paintingTaskRef);
                        paintingTaskRef.Activate(playerTransform);
                    }
                }
                else
                {
                    // intentionally silent
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
            return;
        }

        // Use trash task's configured min/max range to schedule next
        float minT = Mathf.Max(0f, trashMinTimeToStart);
        float maxT = Mathf.Max(minT, trashMaxTimeToStart);

        nextTaskTimer = Random.Range(minT, maxT);
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
                        }
                        if (t is TrashTask && !trashRepeat)
                        {
                            disabledTaskIndices.Add(idx);
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
        int trashIdx = tasks.IndexOf(trashTaskRef);
        if (trashTaskRef == null || trashIdx < 0)
        {
            return false;
        }

        if (disabledTaskIndices != null && disabledTaskIndices.Contains(trashIdx))
        {
            return false;
        }

        // Build list of candidate groups that actually contain at least one non-null transform
        var groups = new List<Transform[]>();
        if (HasNonNull(trashSpawnGroupA)) groups.Add(trashSpawnGroupA);
        if (HasNonNull(trashSpawnGroupB)) groups.Add(trashSpawnGroupB);
        if (HasNonNull(trashSpawnGroupC)) groups.Add(trashSpawnGroupC);

        Transform[] chosenGroup = null;
        string groupName = null;
        if (groups.Count > 0)
        {
            chosenGroup = groups[Random.Range(0, groups.Count)];
            // identify which concrete group we picked for naming
            if (chosenGroup == trashSpawnGroupA) groupName = "Group A";
            else if (chosenGroup == trashSpawnGroupB) groupName = "Group B";
            else if (chosenGroup == trashSpawnGroupC) groupName = "Group C";
            else groupName = "Group (unknown source)";
        }
        else if (trashSpawnPoints != null && trashSpawnPoints.Length > 0)
        {
            if (HasNonNull(trashSpawnPoints))
            {
                chosenGroup = trashSpawnPoints;
                groupName = "Legacy";
            }
        }

        if (chosenGroup == null || chosenGroup.Length == 0)
        {
            return false;
        }

        // remember last chosen group name for external readers (designer scripts)
        LastTrashGroupName = groupName;

        if (trashTaskRef is TrashTask tt)
        {
            tt.SetPlannedGroup(chosenGroup, groupName);
        }

        // Verify the task can activate (plannedGroup will be considered) and activate it
        if (trashTaskRef.CanActivate(playerTransform))
        {
            // ensure we don't add the same task multiple times
            if (!activeTasks.Contains(trashTaskRef))
            {
                activeTasks.Add(trashTaskRef);
                trashTaskRef.Activate(playerTransform);

                // If trash is non-repeatable, mark it disabled immediately after activation
                if (!trashRepeat)
                {
                    if (disabledTaskIndices == null) disabledTaskIndices = new System.Collections.Generic.HashSet<int>();
                    disabledTaskIndices.Add(trashIdx);
                }
            }
            return true;
        }
        else
        {
            if (trashTaskRef is TrashTask tt2) tt2.SetPlannedGroup(null, null);
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
        nextTaskTimer = 0f;
    }

    [ContextMenu("Trigger Painting Now")]
    public void DebugTriggerPaintingNow()
    {
        if (paintingTaskRef != null && paintingTaskRef.CanActivate(playerTransform))
        {
            if (!activeTasks.Contains(paintingTaskRef))
            {
                activeTasks.Add(paintingTaskRef);
                paintingTaskRef.Activate(playerTransform);
            }
        }
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