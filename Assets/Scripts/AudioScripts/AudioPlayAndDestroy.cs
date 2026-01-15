using UnityEngine;

/// <summary>
/// Attach to a GameObject with an AudioSource.
/// Plays the AudioSource on Awake and destroys the GameObject after the clip finishes.
/// Useful for one-shot sound effects that need to play independently of the triggering object.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayAndDestroy : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Additional delay after the audio clip finishes before destroying this GameObject.")]
    [SerializeField] private float extraDelay = 0f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            Debug.LogWarning("[AudioPlayAndDestroy] No AudioSource found on this GameObject. Destroying immediately.");
            Destroy(gameObject);
            return;
        }

        if (audioSource.clip == null)
        {
            Debug.LogWarning("[AudioPlayAndDestroy] AudioSource has no clip assigned. Destroying immediately.");
            Destroy(gameObject);
            return;
        }

        // AudioSource with PlayOnAwake will start automatically
        // Schedule destruction after clip length + extra delay
        float destroyTime = audioSource.clip.length + extraDelay;
        Destroy(gameObject, destroyTime);
        
        Debug.Log($"[AudioPlayAndDestroy] Playing audio '{audioSource.clip.name}' and will destroy in {destroyTime}s");
    }
}
