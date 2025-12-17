using System;
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

    [Tooltip("Optional: assign the CamImage (e.g. B2) to be used by the single painting fall task. If null the script will look up by SpecialCamName.")]
    public CamImage specialCamImage;

    [Header("Lookup names / tags (used when inspector references are missing)")]
    [Tooltip("Name of the camera GameObject to search for (exact name). Leave empty to fallback to the first CamImage found.")]
    public string specialCamName = "BigHall";
    [Tooltip("Tag used for the special painting object (set on the painting GameObject).")]
    public string specialPaintingTag = "SpecialPainting";
    [Tooltip("Tag for Trash Group A objects.")]
    public string trashGroupATag = "TrashGroupA";
    [Tooltip("Tag for Trash Group B objects.")]
    public string trashGroupBTag = "TrashGroupB";
    [Tooltip("Tag for Trash Group C objects.")]
    public string trashGroupCTag = "TrashGroupC";

    [Tooltip("If true the special phone-delay will only start once specialPhoneCallStarted is set to true. Leave false to start the delay immediately.")]
    public bool specialPhoneUseCall = false;

    [HideInInspector] public bool specialPhoneCallStarted = false;

    // runtime
    public List<ITask> tasks = new();
    private List<ITask> activeTasks = new();
    private float nextTaskTimer = 0f;

    public IReadOnlyList<ITask> ActiveTasks => activeTasks.AsReadOnly();

    public event Action OnTasksChanged;

    // runtime direct refs
    private ITask paintingTaskRef;
    private ITask trashTaskRef;
    private ITask singlePaintingTaskRef;

    public List<string> completedTasks = new List<string>();

    private System.Collections.Generic.HashSet<int> disabledTaskIndices;

    public string LastTrashGroupName { get; private set; }

    private void Awake()
    {
        // Initialize runtime collections but do NOT clear inspector-assigned fields.
        activeTasks = new List<ITask>();
        tasks = new List<ITask>();
        completedTasks = new List<string>();
        disabledTaskIndices = new System.Collections.Generic.HashSet<int>();
        nextTaskTimer = 0f;
        LastTrashGroupName = null;

        // try to find player manager if not assigned
        if (playerManager == null)
            playerManager = UnityEngine.Object.FindFirstObjectByType<PlayerManager>();

        // ensure playerTransform follows PlayerManager if available (do not overwrite inspector if present)
        if (playerManager != null && playerTransform == null)
            playerTransform = playerManager.transform;

        // ensure task name defaults
        if (string.IsNullOrWhiteSpace(PaintingTaskName)) PaintingTaskName = "PaintingTask";
        if (string.IsNullOrWhiteSpace(TrashTaskName)) TrashTaskName = "TrashTask";

        // Recover inspector references if they are missing after reload
        RestoreInspectorReferencesIfMissing();

        // If user made trash spawn objects DontDestroyOnLoad they may persist across scene loads.
        // Ensure spawn-group arrays are repopulated from tags when necessary (eg. scene reloads).
        PopulateTrashGroupsFromTagsIfNeeded();

        // create and register fresh task instances
        var paintingTask = new PaintingTask();
        paintingTask.Initialize(this);

        var trashTask = new TrashTask();
        trashTask.Initialize(this);

        var singlePaintingTask = new SinglePaintingFallTask();
        singlePaintingTask.Initialize(this);

        tasks.Add(singlePaintingTask);
        tasks.Add(paintingTask);
        tasks.Add(trashTask);

        singlePaintingTaskRef = singlePaintingTask;
        paintingTaskRef = paintingTask;
        trashTaskRef = trashTask;

        // schedule initial trash timer
        ScheduleNextTask();
    }

    private void Start()
    {
        TryStartInitialTasks();
    }

    private void RestoreInspectorReferencesIfMissing()
    {
        // 1) Special painting: prefer inspector-assigned, otherwise find by configured tag
        if (specialPaintingTarget == null)
        {
            if (!string.IsNullOrWhiteSpace(specialPaintingTag))
            {
                var go = GameObject.FindWithTag(specialPaintingTag);
                if (go != null) specialPaintingTarget = go.transform;
            }

            if (specialPaintingTarget == null)
            {
                // fallback: use first PaintingFallConfig in scene
                var cfg = UnityEngine.Object.FindFirstObjectByType<PaintingFallConfig>();
                if (cfg != null) specialPaintingTarget = cfg.transform;
            }
        }

        // 2) Special camera: if inspector not set, use name provided in specialCamName then fallback to first CamImage
        if (specialCamImage == null)
        {
            if (!string.IsNullOrWhiteSpace(specialCamName))
            {
                var camGO = GameObject.Find(specialCamName);
                if (camGO != null) specialCamImage = camGO.GetComponent<CamImage>();
            }

            if (specialCamImage == null)
                specialCamImage = UnityEngine.Object.FindFirstObjectByType<CamImage>();
        }

        // 3) Trash spawn groups: if inspector arrays empty/null, populate from tag-based groups (A/B/C)
        bool groupsHaveAny = HasNonNull(trashSpawnGroupA) || HasNonNull(trashSpawnGroupB) || HasNonNull(trashSpawnGroupC);
        bool legacyHasAny = trashSpawnPoints != null && trashSpawnPoints.Length > 0 && HasNonNull(trashSpawnPoints);

        if (!groupsHaveAny && !legacyHasAny)
        {
            // Try group A
            if (!string.IsNullOrWhiteSpace(trashGroupATag) && TagExists(trashGroupATag))
            {
                var goA = GameObject.FindGameObjectsWithTag(trashGroupATag);
                if (goA != null && goA.Length > 0)
                {
                    var list = new List<Transform>();
                    foreach (var g in goA) if (g != null) list.Add(g.transform);
                    trashSpawnGroupA = list.ToArray();
                }
            }

            // Try group B
            if (!HasNonNull(trashSpawnGroupB) && !string.IsNullOrWhiteSpace(trashGroupBTag) && TagExists(trashGroupBTag))
            {
                var goB = GameObject.FindGameObjectsWithTag(trashGroupBTag);
                if (goB != null && goB.Length > 0)
                {
                    var list = new List<Transform>();
                    foreach (var g in goB) if (g != null) list.Add(g.transform);
                    trashSpawnGroupB = list.ToArray();
                }
            }

            // Try group C
            if (!HasNonNull(trashSpawnGroupC) && !string.IsNullOrWhiteSpace(trashGroupCTag) && TagExists(trashGroupCTag))
            {
                var goC = GameObject.FindGameObjectsWithTag(trashGroupCTag);
                if (goC != null && goC.Length > 0)
                {
                    var list = new List<Transform>();
                    foreach (var g in goC) if (g != null) list.Add(g.transform);
                    trashSpawnGroupC = list.ToArray();
                }
            }

            // Final fallback: try to fill legacy trashSpawnPoints from any found by tag/name/component
            if ((trashSpawnGroupA == null || trashSpawnGroupA.Length == 0) &&
                (trashSpawnGroupB == null || trashSpawnGroupB.Length == 0) &&
                (trashSpawnGroupC == null || trashSpawnGroupC.Length == 0))
            {
                var candidates = new List<Transform>();
                // try tag-based generic "TrashSpawn" if present
                if (TagExists("TrashSpawn"))
                {
                    var tagged = GameObject.FindGameObjectsWithTag("TrashSpawn");
                    foreach (var g in tagged) if (g != null) candidates.Add(g.transform);
                }

                // fallback: any transform with TrashSpawnConfig or name containing "trash" & "spawn"
                if (candidates.Count == 0)
                {
                    var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();
                    foreach (var tr in allTransforms)
                    {
                        if (tr == null) continue;
                        var nameLower = tr.name.ToLowerInvariant();
                        if (nameLower.Contains("trash") && nameLower.Contains("spawn"))
                        {
                            candidates.Add(tr);
                        }
                        else if (tr.GetComponent<TrashSpawnConfig>() != null)
                        {
                            candidates.Add(tr);
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    trashSpawnPoints = candidates.ToArray();
                }
            }
        }
    }

    // Helper: check whether a tag exists in the current project (safe guard for GameObject.FindGameObjectsWithTag)
    private bool TagExists(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        try
        {
            // This will throw if tag doesn't exist; catch and return false
            GameObject.FindGameObjectsWithTag(tag);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensure trash spawn groups are (re)filled from tagged GameObjects when needed.
    /// This helps when trash objects are marked DontDestroyOnLoad and the scene is reloaded.
    /// We only overwrite a group if it contains no non-null entries.
    /// </summary>
    private void PopulateTrashGroupsFromTagsIfNeeded()
    {
        // Group A
        if (!string.IsNullOrWhiteSpace(trashGroupATag) &&
            (trashSpawnGroupA == null || !HasNonNull(trashSpawnGroupA)) &&
            TagExists(trashGroupATag))
        {
            var goA = GameObject.FindGameObjectsWithTag(trashGroupATag);
            if (goA != null && goA.Length > 0)
            {
                var list = new List<Transform>();
                foreach (var g in goA) if (g != null) list.Add(g.transform);
                trashSpawnGroupA = list.ToArray();
            }
        }

        // Group B
        if (!string.IsNullOrWhiteSpace(trashGroupBTag) &&
            (trashSpawnGroupB == null || !HasNonNull(trashSpawnGroupB)) &&
            TagExists(trashGroupBTag))
        {
            var goB = GameObject.FindGameObjectsWithTag(trashGroupBTag);
            if (goB != null && goB.Length > 0)
            {
                var list = new List<Transform>();
                foreach (var g in goB) if (g != null) list.Add(g.transform);
                trashSpawnGroupB = list.ToArray();
            }
        }

        // Group C
        if (!string.IsNullOrWhiteSpace(trashGroupCTag) &&
            (trashSpawnGroupC == null || !HasNonNull(trashSpawnGroupC)) &&
            TagExists(trashGroupCTag))
        {
            var goC = GameObject.FindGameObjectsWithTag(trashGroupCTag);
            if (goC != null && goC.Length > 0)
            {
                var list = new List<Transform>();
                foreach (var g in goC) if (g != null) list.Add(g.transform);
                trashSpawnGroupC = list.ToArray();
            }
        }

        // If no groups were found, try to fill legacy trashSpawnPoints (only if empty)
        if ((trashSpawnGroupA == null || trashSpawnGroupA.Length == 0) &&
            (trashSpawnGroupB == null || trashSpawnGroupB.Length == 0) &&
            (trashSpawnGroupC == null || trashSpawnGroupC.Length == 0) &&
            (trashSpawnPoints == null || !HasNonNull(trashSpawnPoints)))
        {
            var candidates = new List<Transform>();
            if (TagExists("TrashSpawn"))
            {
                var tagged = GameObject.FindGameObjectsWithTag("TrashSpawn");
                foreach (var g in tagged) if (g != null) candidates.Add(g.transform);
            }

            if (candidates.Count == 0)
            {
                var allTransforms = UnityEngine.Object.FindObjectsOfType<Transform>();
                foreach (var tr in allTransforms)
                {
                    if (tr == null) continue;
                    var nameLower = tr.name.ToLowerInvariant();
                    if (nameLower.Contains("trash") && nameLower.Contains("spawn"))
                    {
                        candidates.Add(tr);
                    }
                    else if (tr.GetComponent<TrashSpawnConfig>() != null)
                    {
                        candidates.Add(tr);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                trashSpawnPoints = candidates.ToArray();
            }
        }
    }

    private void TryStartInitialTasks()
    {
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
                        OnTasksChanged?.Invoke();
                    }
                }
            }
        }

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
                        OnTasksChanged?.Invoke();
                    }
                }
            }
        }
    }

    private void ScheduleNextTask()
    {
        if (disabledTaskIndices != null && disabledTaskIndices.Contains(tasks.IndexOf(trashTaskRef)))
        {
            nextTaskTimer = 0f;
            return;
        }

        float minT = Mathf.Max(0f, trashMinTimeToStart);
        float maxT = Mathf.Max(minT, trashMaxTimeToStart);
        nextTaskTimer = UnityEngine.Random.Range(minT, maxT);
    }

    private void Update()
    {
        if (activeTasks != null && activeTasks.Count > 0)
        {
            for (int i = activeTasks.Count - 1; i >= 0; i--)
            {
                var t = activeTasks[i];
                if (t == null)
                {
                    activeTasks.RemoveAt(i);
                    OnTasksChanged?.Invoke();
                    continue;
                }

                t.Tick(playerTransform);

                if (t.IsCompleted)
                {
                    completedTasks.Add(t.TaskName);
                    var checker = UnityEngine.Object.FindAnyObjectByType<TaskChecker>();
                    if (checker != null) checker.CheckCompletedTasks();

                    int idx = tasks.IndexOf(t);
                    if (idx >= 0)
                    {
                        if (t is PaintingTask && !paintingRepeat) disabledTaskIndices.Add(idx);
                        if (t is TrashTask && !trashRepeat) disabledTaskIndices.Add(idx);
                    }

                    t.Deactivate();
                    activeTasks.RemoveAt(i);
                    OnTasksChanged?.Invoke();
                }
            }
        }

        if (nextTaskTimer > 0f)
        {
            nextTaskTimer -= Time.deltaTime;
            if (nextTaskTimer <= 0f) nextTaskTimer = 0f;
        }

        if (nextTaskTimer <= 0f)
        {
            if (TryStartTrashFromTimer()) ScheduleNextTask();
            else ScheduleNextTask();
        }
    }

    private bool TryStartTrashFromTimer()
    {
        int trashIdx = tasks.IndexOf(trashTaskRef);
        if (trashTaskRef == null || trashIdx < 0) return false;
        if (disabledTaskIndices != null && disabledTaskIndices.Contains(trashIdx)) return false;

        var groups = new List<Transform[]>();
        if (HasNonNull(trashSpawnGroupA)) groups.Add(trashSpawnGroupA);
        if (HasNonNull(trashSpawnGroupB)) groups.Add(trashSpawnGroupB);
        if (HasNonNull(trashSpawnGroupC)) groups.Add(trashSpawnGroupC);

        Transform[] chosenGroup = null;
        string groupName = null;
        if (groups.Count > 0)
        {
            chosenGroup = groups[UnityEngine.Random.Range(0, groups.Count)];
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

        if (chosenGroup == null || chosenGroup.Length == 0) return false;

        LastTrashGroupName = groupName;
        if (trashTaskRef is TrashTask tt) tt.SetPlannedGroup(chosenGroup, groupName);

        if (trashTaskRef.CanActivate(playerTransform))
        {
            if (!activeTasks.Contains(trashTaskRef))
            {
                activeTasks.Add(trashTaskRef);
                trashTaskRef.Activate(playerTransform);
                if (!trashRepeat)
                {
                    if (disabledTaskIndices == null) disabledTaskIndices = new System.Collections.Generic.HashSet<int>();
                    disabledTaskIndices.Add(trashIdx);
                }
                OnTasksChanged?.Invoke();
            }
            return true;
        }
        else
        {
            if (trashTaskRef is TrashTask tt2) tt2.SetPlannedGroup(null, null);
            return false;
        }
    }

    private bool HasNonNull(Transform[] arr)
    {
        if (arr == null || arr.Length == 0) return false;
        foreach (var t in arr) if (t != null) return true;
        return false;
    }

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
                OnTasksChanged?.Invoke();
            }
        }
    }

    public bool InteractPressed()
    {
        if (playerManager != null && playerManager.inputActions != null)
        {
            return playerManager.inputActions.Player.Interact.triggered;
        }

        var kb = Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
    }
}