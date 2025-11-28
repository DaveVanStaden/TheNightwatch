using UnityEngine;

[DisallowMultipleComponent]
public class AccentAudioHolder : MonoBehaviour
{
    [Tooltip("AudioSource attached to this room. If empty, will try to get one from this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("All possible audio clips this room can play.")]
    public AudioClip[] clips = new AudioClip[0];

    [Tooltip("Optional collider that defines the room area. If empty, a spherical radius check is used.")]
    public Collider roomCollider;

    [Tooltip("Fallback radius (meters) when no collider is present.")]
    public float fallbackRadius = 3f;

    // index of the clip last played from this holder (-1 = none)
    [HideInInspector] public int lastPlayedIndex = -1;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (roomCollider == null)
            roomCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// Returns true if the provided camera (player) is inside this holder's room.
    /// Uses the collider bounds if available, otherwise a distance check to this transform.
    /// </summary>
    public bool IsPlayerInside(Camera cam)
    {
        if (cam == null) return false;
        Vector3 playerPos = cam.transform.position;

        if (roomCollider != null)
        {
            return roomCollider.bounds.Contains(playerPos);
        }
        else
        {
            return Vector3.Distance(transform.position, playerPos) <= Mathf.Max(0.001f, fallbackRadius);
        }
    }

    /// <summary>
    /// Returns a random clip index that is NOT equal to lastPlayedIndex.
    /// Returns -1 if no valid index is available.
    /// </summary>
    public int GetRandomClipIndexExcludingLast()
    {
        if (clips == null || clips.Length == 0) return -1;
        if (clips.Length == 1)
        {
            return lastPlayedIndex == 0 ? -1 : 0;
        }

        int attempts = 8;
        int idx;
        do
        {
            idx = Random.Range(0, clips.Length);
            attempts--;
        } while (idx == lastPlayedIndex && attempts > 0);

        if (idx == lastPlayedIndex)
        {
            // fallback: try to pick any different index deterministically
            for (int i = 0; i < clips.Length; i++)
            {
                if (i != lastPlayedIndex)
                {
                    idx = i;
                    break;
                }
            }
            if (idx == lastPlayedIndex) return -1;
        }

        return idx;
    }

    /// <summary>
    /// Play the clip at index using the configured AudioSource (PlayOneShot).
    /// Updates lastPlayedIndex so the manager can enforce the single-previous cooldown.
    /// </summary>
    public void PlayClipIndex(int index)
    {
        if (clips == null || index < 0 || index >= clips.Length) return;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clips[index]);
            lastPlayedIndex = index;
        }
        else
        {
            // fallback: play at position (no persistent AudioSource)
            AudioSource.PlayClipAtPoint(clips[index], transform.position);
            lastPlayedIndex = index;
        }
    }

    /// <summary>
    /// Returns true if this holder has at least one clip that can be played
    /// (i.e. clips.Length > 0 and at least one clip is not currently blocked by lastPlayedIndex).
    /// </summary>
    public bool HasAvailableClip()
    {
        if (clips == null || clips.Length == 0) return false;
        if (clips.Length == 1) return lastPlayedIndex != 0;
        return true;
    }
}
