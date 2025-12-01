using UnityEngine;

/// <summary>
/// Trash task implemented without MonoBehaviour so it can be driven by TaskManager.
/// Spawn/cleanup handled by TaskManager-provided prefab + points.
/// </summary>
public class TrashTask : ITask
{
    public override string TaskName => "TrashTask";

    private GameObject spawnedTrash;
    private bool completed = false;

    public override bool CanActivate(Transform player)
    {
        // activate only if we have spawn points and a prefab via manager and at least one spawn point is usable (player not in that spawn's room)
        if (manager == null) return false;
        if (manager.trashPrefab == null) return false;
        if (manager.trashSpawnPoints == null || manager.trashSpawnPoints.Length == 0) return false;
        if (spawnedTrash != null) return false;
        if (player == null) return false;

        // Note: do NOT block activation permanently based on the previous 'completed' value.
        // The task instance is reused by TaskManager, so completed is only a per-run flag.
        foreach (var sp in manager.trashSpawnPoints)
        {
            if (sp == null) continue;
            if (!IsPlayerInRoomForSpawn(sp, player))
                return true;
        }

        return false;
    }

    public override void Activate(Transform player)
    {
        // reset per-run completion flag
        completed = false;

        if (manager == null || manager.trashPrefab == null || manager.trashSpawnPoints == null) { completed = true; return; }

        // filter spawn points that are not in the player's current room
        var usable = new System.Collections.Generic.List<Transform>();
        foreach (var sp in manager.trashSpawnPoints)
        {
            if (sp == null) continue;
            if (!IsPlayerInRoomForSpawn(sp, player))
                usable.Add(sp);
        }

        if (usable.Count == 0) { completed = true; return; }

        int idx = Random.Range(0, usable.Count);
        Transform spChosen = usable[idx];
        if (spChosen == null) { completed = true; return; }

        spawnedTrash = Object.Instantiate(manager.trashPrefab, spChosen.position, spChosen.rotation);
        completed = false;
    }

    public override void Tick(Transform player)
    {
        if (completed || spawnedTrash == null || player == null) return;

        float d = Vector3.Distance(player.position, spawnedTrash.transform.position);

        // Simple interaction: press Interact when within range to clean up.
        if (d <= manager.trashInteractionDistance && manager != null && manager.InteractPressed())
        {
            CleanUp();
        }
    }

    private void CleanUp()
    {
        if (spawnedTrash != null)
        {
            Object.Destroy(spawnedTrash);
            spawnedTrash = null;
        }
        completed = true;
    }

    public override void Deactivate()
    {
        // ensure any spawned object is cleaned up and reset per-run state so task can be reused
        if (spawnedTrash != null)
        {
            Object.Destroy(spawnedTrash);
            spawnedTrash = null;
        }
        completed = false;
    }

    public override bool IsCompleted => completed;

    private bool IsPlayerInRoomForSpawn(Transform spawnPoint, Transform player)
    {
        if (spawnPoint == null || player == null) return false;
        var cfg = spawnPoint.GetComponent<TrashSpawnConfig>();
        if (cfg == null || cfg.roomBounds == null) return false;
        var col = cfg.roomBounds.GetComponent<Collider>();
        if (col == null) return false;
        return col.bounds.Contains(player.position);
    }
}
