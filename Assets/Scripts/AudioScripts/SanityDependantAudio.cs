using System.Collections.Generic;
using UnityEngine;

public class SanityDependantAudio : MonoBehaviour
{
    [Header("Volume Settings")]
    [Tooltip("Volume when sanity is at or above max sanity threshold")]
    [Range(0f, 1f)]
    public float minVolume = 0f;

    [Tooltip("Volume when sanity is at or below min sanity threshold")]
    [Range(0f, 1f)]
    public float maxVolume = 0.8f;

    [Header("Sanity Thresholds")]
    [Tooltip("Sanity value for minimum volume (higher sanity = quieter)")]
    [Range(0, 100)]
    public int maxSanityThreshold = 80;

    [Tooltip("Sanity value for maximum volume (lower sanity = louder)")]
    [Range(0, 100)]
    public int minSanityThreshold = 30;

    [Header("Audio Sources")]
    [Tooltip("List of AudioSources to control volume based on sanity")]
    public List<AudioSource> audioSources = new List<AudioSource>();

    [Header("Settings")]
    [Tooltip("How quickly the volume transitions (0 = instant, higher = smoother)")]
    [Range(0f, 10f)]
    public float smoothing = 2f;

    private Component playerStatsComponent;
    private System.Reflection.PropertyInfo sanityProperty;
    private float targetVolume;

    private void Start()
    {
        var playerStatsType = System.Type.GetType("PlayerStats");
        if (playerStatsType != null)
        {
            var instanceProp = playerStatsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instanceProp != null)
            {
                playerStatsComponent = instanceProp.GetValue(null, null) as Component;
            }
            else
            {
                playerStatsComponent = Object.FindAnyObjectByType(playerStatsType) as Component;
            }

            if (playerStatsComponent != null)
            {
                sanityProperty = playerStatsType.GetProperty("Sanity");
            }
        }

        foreach (var audioSource in audioSources)
        {
            if (audioSource != null && !audioSource.isPlaying && audioSource.playOnAwake)
            {
                audioSource.Play();
            }
        }
    }

    private void Update()
    {
        if (playerStatsComponent == null || sanityProperty == null) return;

        int currentSanity = (int)sanityProperty.GetValue(playerStatsComponent, null);

        float t = Mathf.InverseLerp(maxSanityThreshold, minSanityThreshold, currentSanity);
        targetVolume = Mathf.Lerp(minVolume, maxVolume, t);

        foreach (var audioSource in audioSources)
        {
            if (audioSource == null) continue;

            if (smoothing > 0f)
            {
                audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * smoothing);
            }
            else
            {
                audioSource.volume = targetVolume;
            }
        }
    }
}
