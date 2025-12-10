using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages ambient/accent audio per room (AccentAudioHolder).
/// Picks a random room the player is NOT in (unless gating disabled), chooses a clip
/// that is not the previous one for that room and plays it. Timing between plays is randomized
/// between minInterval and maxInterval (inspector exposed).
/// </summary>
public class AccentAudioManager : MonoBehaviour
{
    [Tooltip("All room holders to manage. If left empty the manager will auto-discover all AccentAudioHolder in scene.")]
    public List<AccentAudioHolder> holders = new List<AccentAudioHolder>();

    [Header("Timing")]
    [Tooltip("Minimum seconds between random plays.")]
    public float minInterval = 10f;
    [Tooltip("Maximum seconds between random plays.")]
    public float maxInterval = 30f;

    [Header("Behavior")]
    [Tooltip("If true, the manager will only play sounds in rooms the player is NOT currently inside.")]
    public bool requirePlayerNotInRoom = true;

    [Tooltip("Player camera used for visibility/inside checks. If null, Camera.main is used.")]
    public Camera playerCamera;

    [Tooltip("Enable verbose debug logs.")]
    public bool debug = false;

    private Coroutine loopCoroutine;

    void OnEnable()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (holders == null || holders.Count == 0)
            AutoDiscoverHolders();

        loopCoroutine = StartCoroutine(LoopRoutine());
    }

    void OnDisable()
    {
        if (loopCoroutine != null)
            StopCoroutine(loopCoroutine);
    }

    private void AutoDiscoverHolders()
    {
        holders = new List<AccentAudioHolder>(FindObjectsByType<AccentAudioHolder>(FindObjectsSortMode.None));
        if (debug) Debug.Log($"[AccentAudioManager] Auto-discovered {holders.Count} holders.");
    }

    private IEnumerator LoopRoutine()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    if (debug) Debug.LogWarning("[AccentAudioManager] No player camera; skipping this cycle.");
                    continue;
                }
            }

            // refresh holders if inspector list empty or a count mismatch occurred
            if (holders == null || holders.Count == 0)
                AutoDiscoverHolders();

            // build candidate list
            List<AccentAudioHolder> candidates = new List<AccentAudioHolder>(holders.Count);
            foreach (var h in holders)
            {
                if (h == null) continue;
                // must have at least one playable clip
                if (!h.HasAvailableClip()) continue;

                bool playerInside = h.IsPlayerInside(playerCamera);
                if (requirePlayerNotInRoom && playerInside) continue;

                candidates.Add(h);
            }

            if (candidates.Count == 0)
            {
                if (debug) Debug.Log("[AccentAudioManager] No candidate rooms available this cycle.");
                continue;
            }

            // pick random holder
            var chosenHolder = candidates[Random.Range(0, candidates.Count)];
            int clipIndex = chosenHolder.GetRandomClipIndexExcludingLast();
            if (clipIndex < 0)
            {
                if (debug) Debug.Log($"[AccentAudioManager] Holder '{chosenHolder.name}' had no valid clip to play (all blocked).");
                continue;
            }

            chosenHolder.PlayClipIndex(clipIndex);
            if (debug) Debug.Log($"[AccentAudioManager] Played clip index {clipIndex} in '{chosenHolder.name}'. Next interval randomized.");
            // next loop iteration will wait a new randomized interval
        }
    }
}
