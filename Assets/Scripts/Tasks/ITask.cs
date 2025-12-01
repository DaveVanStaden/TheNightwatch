using UnityEngine;

/// <summary>
/// Base task contract. Tasks are plain classes (not MonoBehaviours) driven by TaskManager.
/// </summary>
public abstract class ITask
{
    protected TaskManager manager;

    /// <summary>Human readable name for logs/UI.</summary>
    public abstract string TaskName { get; }

    /// <summary>Called once by TaskManager to give the task a backreference.</summary>
    public virtual void Initialize(TaskManager manager)
    {
        this.manager = manager;
    }

    /// <summary>Return true if task can activate right now given player's transform.</summary>
    public abstract bool CanActivate(Transform player);

    /// <summary>Called by TaskManager when this task becomes active.</summary>
    public abstract void Activate(Transform player);

    /// <summary>Called each frame while this task is active.</summary>
    public abstract void Tick(Transform player);

    /// <summary>Called by TaskManager if the task is forcefully cancelled / deactivated.</summary>
    public abstract void Deactivate();

    /// <summary>True when the task has finished its work and can be recorded as complete.</summary>
    public abstract bool IsCompleted { get; }
}