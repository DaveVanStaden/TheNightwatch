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

    // runtime
    private List<ITask> tasks = new List<ITask>();
    private ITask activeTask = null;
    private float nextTaskTimer = 0f;

    // simple tracking
    public List<string> completedTasks = new List<string>();

    private void Awake()
    {
        if (playerManager == null)
            playerManager = FindObjectOfType<PlayerManager>();

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

        // create task instances and initialize them
        var paintingTask = new PaintingTask();
        paintingTask.Initialize(this);

        var trashTask = new TrashTask();
        trashTask.Initialize(this);

        tasks.Add(paintingTask);
        tasks.Add(trashTask);

        ScheduleNextTask();
    }

    private void ScheduleNextTask()
    {
        nextTaskTimer = Random.Range(minTimeToNextTask, maxTimeToNextTask);
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
                activeTask.Deactivate();
                activeTask = null;
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

        // randomize order
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
            var t = tasks[idx];
            if (t == null) continue;
            if (t.CanActivate(playerTransform))
            {
                activeTask = t;
                activeTask.Activate(playerTransform);
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