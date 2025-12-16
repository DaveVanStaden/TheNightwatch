using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Trash task implemented without MonoBehaviour so it can be driven by TaskManager.
/// Spawn/cleanup handled by TaskManager-provided prefab + points.
///
/// Changes:
/// - Supports an externally-planned spawn group via SetPlannedGroup(...) so TaskManager can choose
///   which of the 3 groups to use when the timer expires.
/// - Removed "player in room" restrictions. Trash spawn groups and legacy spawn list are used directly.
/// - When a group is chosen, trash is spawned at ALL transforms in that group (skips null entries).
/// - Legacy fallback now spawns at all legacy spawn points (keeps backward compatibility).
/// - CanActivate no longer depends on player being outside a spawn's room.
/// </summary>
public class TrashTask : ITask
{
    private string taskName = "TrashTask";
    public override string TaskName => taskName;

    // externally planned group (set by TaskManager before Activate); if null the task chooses randomly
    private Transform[] plannedGroup = null;

    // store a human-readable name for the planned/selected group (designer scripts can read this)
    public string PlannedGroupName { get; private set; } = null;

    // multiple spawned trash objects (one per spawn point in chosen group)
    private List<GameObject> spawnedTrash = new List<GameObject>();
    private bool completed = false;

    public override void Initialize(TaskManager manager)
    {
        this.manager = manager;
        if (manager != null && !string.IsNullOrWhiteSpace(manager.TrashTaskName))
            taskName = manager.TrashTaskName;
        else
            taskName = "TrashTask";
    }

    /// <summary>
    /// Allow TaskManager to explicitly pick which group to use when the timer fires.
    /// Pass null to clear planned selection and allow the task to choose randomly on activation.
    /// Optional groupName is a human readable identifier (e.g. "Group A", "Legacy") supplied by TaskManager.
    /// </summary>
    public void SetPlannedGroup(Transform[] group, string groupName = null)
    {
        plannedGroup = group;
        PlannedGroupName = groupName;
        Debug.Log($"[TrashTask] Planned group set ({(group == null ? "null" : group.Length.ToString())} entries) name={(groupName ?? "null")}");
    }

    public override bool CanActivate(Transform player)
    {
        if (manager == null) return false;
        if (manager.trashPrefab == null) return false;
        if (spawnedTrash.Count > 0) return false; // already active/spawned

        // If a plannedGroup exists, ensure it has at least one non-null entry
        if (plannedGroup != null && plannedGroup.Length > 0)
        {
            foreach (var sp in plannedGroup) if (sp != null) return true;
            return false;
        }

        // If any configured group has at least one spawn point, we can activate.
        if (HasSpawnInGroup(manager.trashSpawnGroupA)) return true;
        if (HasSpawnInGroup(manager.trashSpawnGroupB)) return true;
        if (HasSpawnInGroup(manager.trashSpawnGroupC)) return true;

        // Fallback to legacy single-array (if configured)
        if (manager.trashSpawnPoints != null && manager.trashSpawnPoints.Length > 0)
            return true;

        return false;
    }

    public override void Activate(Transform player)
    {
        // reset per-run completion flag
        completed = false;
        spawnedTrash.Clear();

        if (manager == null || manager.trashPrefab == null) { completed = true; return; }

        Debug.Log("[TrashTask] Activate called");

        // If a planned group was provided by TaskManager, use it deterministically.
        if (plannedGroup != null && plannedGroup.Length > 0)
        {
            Debug.Log($"[TrashTask] Using planned group with {plannedGroup.Length} entries (name={PlannedGroupName ?? "null"})");
            foreach (var sp in plannedGroup)
            {
                if (sp == null) continue;
                var go = Object.Instantiate(manager.trashPrefab, sp.position, sp.rotation);
                spawnedTrash.Add(go);
            }
            // Clear planned selection after use so subsequent activations behave normally
            // Keep PlannedGroupName set while active so designer UI can read it; clear plannedGroup reference.
            plannedGroup = null;
            Debug.Log($"[TrashTask] Spawned {spawnedTrash.Count} trash from planned group");
            completed = spawnedTrash.Count == 0;
            return;
        }

        // Choose one of the groups randomly among those that have at least one spawn point
        var candidateGroups = new List<Transform[]>();

        if (HasSpawnInGroup(manager.trashSpawnGroupA)) candidateGroups.Add(manager.trashSpawnGroupA);
        if (HasSpawnInGroup(manager.trashSpawnGroupB)) candidateGroups.Add(manager.trashSpawnGroupB);
        if (HasSpawnInGroup(manager.trashSpawnGroupC)) candidateGroups.Add(manager.trashSpawnGroupC);

        // If no explicit groups chosen, fall back to legacy single-array if present
        if (candidateGroups.Count == 0 && manager.trashSpawnPoints != null && manager.trashSpawnPoints.Length > 0)
        {
            Debug.Log("[TrashTask] Using legacy trashSpawnPoints fallback");
            PlannedGroupName = "Legacy";
            foreach (var sp in manager.trashSpawnPoints)
            {
                if (sp == null) continue;
                var go = Object.Instantiate(manager.trashPrefab, sp.position, sp.rotation);
                spawnedTrash.Add(go);
            }

            Debug.Log($"[TrashTask] Spawned {spawnedTrash.Count} trash from legacy points");
            completed = spawnedTrash.Count == 0;
            return;
        }

        if (candidateGroups.Count == 0)
        {
            // nothing configured
            Debug.Log("[TrashTask] No candidate groups available - nothing spawned");
            completed = true;
            return;
        }

        // pick a random candidate group
        var chosenGroup = candidateGroups[Random.Range(0, candidateGroups.Count)];
        if (chosenGroup == null || chosenGroup.Length == 0) { completed = true; return; }

        // attempt to set a human-readable name for the chosen group
        if (chosenGroup == manager.trashSpawnGroupA) PlannedGroupName = "Group A";
        else if (chosenGroup == manager.trashSpawnGroupB) PlannedGroupName = "Group B";
        else if (chosenGroup == manager.trashSpawnGroupC) PlannedGroupName = "Group C";
        else PlannedGroupName = "Group (unknown)";

        Debug.Log($"[TrashTask] Randomly chosen group has {chosenGroup.Length} entries (name={PlannedGroupName})");
        // spawn trash at all spawn points in chosenGroup (skip null entries)
        foreach (var sp in chosenGroup)
        {
            if (sp == null) continue;
            var go = Object.Instantiate(manager.trashPrefab, sp.position, sp.rotation);
            spawnedTrash.Add(go);
        }

        // if nothing spawned (all points were null), mark completed
        Debug.Log($"[TrashTask] Spawned {spawnedTrash.Count} trash in Activate");
        completed = spawnedTrash.Count == 0;
    }

    public override void Tick(Transform player)
    {
        if (completed || spawnedTrash == null || spawnedTrash.Count == 0) return;

        // If player interacts near any spawned trash object, clean that one up.
        for (int i = spawnedTrash.Count - 1; i >= 0; i--)
        {
            var trash = spawnedTrash[i];
            if (trash == null)
            {
                spawnedTrash.RemoveAt(i);
                continue;
            }

            float d = Vector3.Distance(player.position, trash.transform.position);
            if (d <= manager.trashInteractionDistance && manager != null && manager.InteractPressed())
            {
                Object.Destroy(trash);
                spawnedTrash.RemoveAt(i);
            }
        }

        // completed when no spawned trash remains
        if (spawnedTrash.Count == 0)
        {
            completed = true;
        }
    }

    public override void Deactivate()
    {
        // ensure any spawned objects are cleaned up and reset per-run state so task can be reused
        if (spawnedTrash != null && spawnedTrash.Count > 0)
        {
            foreach (var go in spawnedTrash)
            {
                if (go != null) Object.Destroy(go);
            }
            spawnedTrash.Clear();
        }
        // clear planned group on deactivate as well
        plannedGroup = null;
        PlannedGroupName = null;
        completed = false;
    }

    public override bool IsCompleted => completed;

    // Helper: returns true if the group has at least one non-null transform
    private bool HasSpawnInGroup(Transform[] group)
    {
        if (group == null || group.Length == 0) return false;
        foreach (var sp in group) if (sp != null) return true;
        return false;
    }
}
