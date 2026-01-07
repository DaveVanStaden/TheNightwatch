using UnityEngine;

/// <summary>
/// Special task that represents the finale sequence.
/// This task activates when all main tasks are complete and completes when the player escapes.
/// </summary>
public class FinaleTask : ITask
{
    private string taskName = "FinaleTask";
    public override string TaskName => taskName;

    private bool completed = false;
    private FinalSequenceManager sequenceManager;

    public override void Initialize(TaskManager manager)
    {
        this.manager = manager;
        taskName = "FinaleTask";

        // Find the FinalSequenceManager
        sequenceManager = Object.FindAnyObjectByType<FinalSequenceManager>();
    }

    public override bool CanActivate(Transform player)
    {
        // Can only activate if all main tasks are complete (checked by FinalSequenceManager)
        return !completed && sequenceManager != null;
    }

    public override void Activate(Transform player)
    {
        completed = false;
        Debug.Log("[FinaleTask] Finale task activated!");
        
        // Notify UI
        manager?.NotifyTasksChanged();
    }

    public override void Tick(Transform player)
    {
        // This task completes when the player reaches the end sequence trigger
        // The FinalSequenceManager will call CompleteFinale() when appropriate
    }

    public override void Deactivate()
    {
        completed = false;
        manager?.NotifyTasksChanged();
    }

    public override bool IsCompleted => completed;

    /// <summary>
    /// Called by FinalSequenceManager when the finale is complete
    /// </summary>
    public void CompleteFinale()
    {
        if (!completed)
        {
            completed = true;
            Debug.Log("[FinaleTask] Finale task completed!");
            manager?.NotifyTasksChanged();
        }
    }
}
