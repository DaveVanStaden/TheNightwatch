using UnityEngine;

/// <summary>
/// Simple component to mark a statue and expose the head pivot that will rotate.
/// Stores the initial local rotation for safe restoration.
/// </summary>
[RequireComponent(typeof(Transform))]
public class Statue : MonoBehaviour
{
    [Tooltip("Transform that should rotate to 'look' at the player (head pivot).")]
    public Transform headTransform;

    // initial local rotation of the head (rest pose)
    private Quaternion initialLocalRotation;

    private void Awake()
    {
        if (headTransform == null)
        {
            // try to find a child named "Head" as a convention
            var t = transform.Find("Head");
            if (t != null) headTransform = t;
        }

        if (headTransform == null)
            headTransform = transform; // fallback to whole object

        initialLocalRotation = headTransform.localRotation;
    }

    public Quaternion GetInitialLocalRotation() => initialLocalRotation;

    public void ResetHeadImmediate()
    {
        if (headTransform != null) headTransform.localRotation = initialLocalRotation;
    }

    public void SetHeadLocalRotation(Quaternion localRot)
    {
        if (headTransform != null) headTransform.localRotation = localRot;
    }

    public Transform GetHeadTransform() => headTransform;
}
