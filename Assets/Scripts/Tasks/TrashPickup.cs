using UnityEngine;

/// <summary>
/// Attach this component to trash prefab GameObjects to handle pickup audio and interaction feedback.
/// The TrashTask will detect this component and trigger the pickup through this script.
/// </summary>
public class TrashPickup : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Audio prefab to spawn at this trash position when picked up. The prefab should play audio on awake and destroy itself after playing.")]
    [SerializeField] private GameObject pickupAudioPrefab;

    private bool isPickedUp = false;

    /// <summary>
    /// Called by TrashTask when the player picks up this trash.
    /// Spawns the audio prefab at this position and marks this trash as picked up.
    /// </summary>
    public void Pickup()
    {
        if (isPickedUp) return;

        isPickedUp = true;

        // Spawn audio prefab at trash position if configured
        if (pickupAudioPrefab != null)
        {
            Instantiate(pickupAudioPrefab, transform.position, Quaternion.identity);
            Debug.Log($"[TrashPickup] Spawned pickup audio prefab at '{gameObject.name}' position");
        }
    }

    /// <summary>
    /// Returns true if this trash has been picked up (used by TrashTask to know when to destroy it).
    /// </summary>
    public bool IsPickedUp => isPickedUp;
}
