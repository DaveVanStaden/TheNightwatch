using UnityEngine;

/// <summary>
/// Attach this to a SecurityCamera or a camera world object. Provide a spawnTransform where the camera-entity should be instantiated.
/// </summary>
public class CameraSpot : MonoBehaviour
{
    [Tooltip("World transform where the CameraEntity will be spawned/parented.")]
    public Transform spawnPoint;

    // Optional reference to the CamGroup this camera belongs to (not required; module finds group via CamImage).
    public CamGroup camGroup;

    private void Reset()
    {
        // If no spawnPoint provided, use this transform as default
        if (spawnPoint == null) spawnPoint = this.transform;
    }
}
