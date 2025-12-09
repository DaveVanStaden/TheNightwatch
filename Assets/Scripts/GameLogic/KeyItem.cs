using UnityEngine;

/// <summary>
/// Simple pickup for keys. Attach to a key GameObject with a Collider (can be a trigger).
/// This version no longer subscribes to input itself — pickups via raycast are handled centrally
/// in PlayerInteraction. OnTriggerEnter is preserved as a fallback for collision pickups.
/// </summary>
public class KeyItem : MonoBehaviour
{
    [Tooltip("Identifier for this key. Match this to Door.requiredKey.")]
    public string keyId = "MasterKey";

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        // prefer PlayerManager presence on the Player GameObject
        var pm = other.GetComponent<PlayerManager>();
        if (pm == null)
        {
            // maybe the collider was on a child; try parent lookup
            pm = other.GetComponentInParent<PlayerManager>();
            if (pm == null) return;
        }

        Pickup(pm, "trigger");
    }

    /// <summary>
    /// Called by PlayerInteraction when the player presses Interact and raycast hits this object.
    /// </summary>
    public void PickupBy(PlayerManager pm, string via = "interaction")
    {
        Pickup(pm, via);
    }

    private void Pickup(PlayerManager pm, string via)
    {
        if (pm == null)
        {
            // Try to resolve a PlayerManager if caller didn't provide one.
            pm = FindAnyObjectByType<PlayerManager>();
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddKey(keyId);
            Debug.Log($"[KeyItem] Player picked up key '{keyId}' via {via}.");
        }
        else
        {
            Debug.LogWarning("[KeyItem] PlayerStats.Instance is null - cannot add key.");
        }

        Destroy(gameObject);
    }
}