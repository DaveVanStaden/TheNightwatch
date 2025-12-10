using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives tasks. This component is the only MonoBehaviour; tasks are plain classes instantiated here.
/// Only one task active at a time. Timing randomized via min/max fields.
/// </summary>
public class TaskManager : MonoBehaviour
{
    [Header("Timing (seconds)")]
    [Tooltip("Minimum time before next task may activate.")]
    public float minTimeToNextTask = 30f;
    [Tooltip("Maximum time before next task may activate.")]
    public float maxTimeToNextTask = 90f;

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

    [Header("Trash Task Settings")]
    [Tooltip("Trash prefab to spawn.")]
    public GameObject trashPrefab;
    [Tooltip("Possible trash spawn locations.")]
    public Transform[] trashSpawnPoints;
    [Tooltip("Distance player must be to interact with trash.")]
    public float trashInteractionDistance = 2.5f;

    [Header("Task names")]
    [Tooltip("Human readable name used for the Painting task (logged/completed list).")]
    public string PaintingTaskName = "PaintingTask";
    [Tooltip("Human readable name used for the Trash task (logged/completed list).")]
    public string TrashTaskName = "TrashTask";

    // runtime
    public  List<ITask> tasks = new();
    private ITask activeTask = null;
    private int activeTaskIndex = -1;         // index of the currently active task in `tasks`
    private int lastActivatedTaskIndex = -1;  // index of the last completed task; skip it once
    private float nextTaskTimer = 0f;

    // simple tracking
    public List<string> completedTasks = new List<string>();

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
        // intentionally silent to avoid console spam

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

        // safe scheduling
        ScheduleNextTask();
    }

    private void ScheduleNextTask()
    {
        nextTaskTimer = Random.Range(minTimeToNextTask, maxTimeToNextTask);
        var checker = FindAnyObjectByType<TaskChecker>();
        if (checker != null)
        {
            checker.CheckTaskList();
        }
        else
        {
            // intentionally silent if no TaskChecker is present
        }
    }

    private void Update()
    {
        // tick active task
        if (activeTask != null)
        {
            activeTask.Tick(playerTransform);

            if (activeTask.IsCompleted)
            {
                completedTasks.Add(activeTask.TaskName);
                var checker = FindAnyObjectByType<TaskChecker>();
                if (checker != null) checker.CheckCompletedTasks();

                // remember which task just finished so it won't be picked immediately next time
                lastActivatedTaskIndex = activeTaskIndex;

                activeTask.Deactivate();
                activeTask = null;
                activeTaskIndex = -1;
                ScheduleNextTask();
            }

            return;
        }

        // no active task: countdown to next activation
        if (nextTaskTimer > 0f)
        {
            nextTaskTimer -= Time.deltaTime;
            return;
        }

        // try to activate a random eligible task
        if (tasks == null || tasks.Count == 0)
        {
            ScheduleNextTask();
            return;
        }

        // randomize order (we store actual task indices so we can compare easily)
        var indices = new List<int>(tasks.Count);
        for (int i = 0; i < tasks.Count; i++) indices.Add(i);
        for (int i = 0; i < indices.Count; i++)
        {
            int j = Random.Range(i, indices.Count);
            int tmp = indices[i];
            indices[i] = indices[j];
            indices[j] = tmp;
        }

        foreach (int idx in indices)
        {
            // skip invalid entries
            if (idx < 0 || idx >= tasks.Count) continue;
            var t = tasks[idx];
            if (t == null) continue;

            // do not pick the same task that just finished
            if (idx == lastActivatedTaskIndex) continue;

            if (t.CanActivate(playerTransform))
            {
                activeTaskIndex = idx;
                activeTask = t;
                activeTask.Activate(playerTransform);

                // once we successfully started a different task, clear the "skip once" marker
                // so the previously completed task can appear again later
                lastActivatedTaskIndex = -1;

                // wait until complete to schedule next
                return;
            }
        }

        // none could activate now -> schedule next attempt
        ScheduleNextTask();
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