using UnityEngine;

/// <summary>
/// Attach to a GameObject with an AudioSource.
/// Plays a random AudioClip from a list and destroys the GameObject after the clip finishes.
/// Useful for one-shot sound effects with variation that need to play independently of the triggering object.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayAndDestroy : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("List of audio clips to randomly choose from. One will be selected and played.")]
    [SerializeField] private AudioClip[] audioClips;

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

        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogWarning("[AudioPlayAndDestroy] No audio clips assigned. Destroying immediately.");
            Destroy(gameObject);
            return;
        }

        // Pick a random clip from the list
        AudioClip selectedClip = audioClips[Random.Range(0, audioClips.Length)];
        
        if (selectedClip == null)
        {
            Debug.LogWarning("[AudioPlayAndDestroy] Selected audio clip is null. Destroying immediately.");
            Destroy(gameObject);
            return;
        }

        // Assign and play the clip
        audioSource.clip = selectedClip;
        audioSource.Play();
        
        // Schedule destruction after clip length + extra delay
        float destroyTime = selectedClip.length + extraDelay;
        Destroy(gameObject, destroyTime);
        
        Debug.Log($"[AudioPlayAndDestroy] Playing random audio '{selectedClip.name}' and will destroy in {destroyTime}s");
    }
}
