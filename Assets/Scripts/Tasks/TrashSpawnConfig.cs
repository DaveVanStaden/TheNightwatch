using UnityEngine;

public class TrashSpawnConfig : MonoBehaviour
{
    [Tooltip("Optional: transform (typically a GameObject with a Collider) that represents the room this spawn point is in. If player's position is inside this collider, the spawn point will be ignored.")]
    public Transform roomBounds;

}
