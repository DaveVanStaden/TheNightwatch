using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ElectricityLogic : MonoBehaviour
{
    [Header("Power settings")]
    [Tooltip("Starting power level (0..100)")]
    [SerializeField] private float startPowerLevel = 100f;
    [Tooltip("Seconds it takes to drain from 100 -> 0 when ALL registered powergroups are active (approx. 5-6 minutes = 330s default)")]
    [SerializeField] private float fullDrainSecondsWhenAllActive = 330f;

    [Header("Power Almost Up Audio")]
    [Tooltip("AudioSource to play the power almost up sound from.")]
    [SerializeField] private AudioSource powerAlmostUpSource;
    [Tooltip("AudioClip to play when power dips below the threshold.")]
    [SerializeField] private AudioClip powerAlmostUpClip;
    [Tooltip("Power threshold - sound plays when power dips below this value (default: 10% = low power warning).")]
    [SerializeField] [Range(0f, 100f)] private float powerAlmostUpThreshold = 10f;

    [Header("Events")]
    [Tooltip("Invoked once when power is depleted. Designer will hook this to disable all powergroups.")]
    public UnityEvent onPowerOut;
    [Tooltip("Invoked when power is restored (ResetBreakerBox called).")]
    public UnityEvent onPowerRestored;

    // public runtime state (0..100)
    public float PowerLevel { get; private set; }

    // true while power is considered out
    public bool IsPowerOut { get; private set; }

    // tracked BreakerButtons (powergroups). Designers can register/unregister at runtime if needed.
    private readonly List<BreakerButton> registeredBreakers = new List<BreakerButton>();

    private bool powerAlmostUpSoundPlayed;

    private void Awake()
    {
        PowerLevel = Mathf.Clamp(startPowerLevel, 0f, 100f);
        IsPowerOut = false;
    }

    private void OnEnable()
    {
        RegisterAllInScene();
    }

    private void Update()
    {
        //Debug.Log(PowerLevel);
        if (IsPowerOut)
            return; 

        for (int i = registeredBreakers.Count - 1; i >= 0; i--)
        {
            if (registeredBreakers[i] == null)
                registeredBreakers.RemoveAt(i);
        }

        int total = registeredBreakers.Count;
        if (total == 0)
            return; // nothing to consume power

        int activeCount = 0;
        for (int i = 0; i < registeredBreakers.Count; i++)
        {
            // use BreakerButton.isOn to judge whether the group is active
            if (registeredBreakers[i].isOn)
                activeCount++;
        }

        if (activeCount == 0)
            return; // no consumption when nothing is active

        // Store previous power level to detect threshold crossing
        float previousPowerLevel = PowerLevel;

        // Drain calculation:
        // If all groups active => drain rate = 100 / fullDrainSecondsWhenAllActive (percent per second).
        // For partial active groups: drain scales linearly by (activeCount / total).
        // This makes total drain = (activeCount/total) * (100 / fullDrainSecondsWhenAllActive) percent per second.
        float drainPerSecond = (activeCount / (float)total) * (100f / Mathf.Max(1e-6f, fullDrainSecondsWhenAllActive));
        PowerLevel -= drainPerSecond * Time.deltaTime;
        PowerLevel = Mathf.Clamp(PowerLevel, 0f, 100f);

        // Check if power dipped below threshold (was above, now below)
        if (!powerAlmostUpSoundPlayed && previousPowerLevel >= powerAlmostUpThreshold && PowerLevel < powerAlmostUpThreshold)
        {
            PlayPowerAlmostUpSound();
        }

        if (PowerLevel <= 0f && !IsPowerOut)
        {
            HandlePowerDepleted();
        }
    }

    private void HandlePowerDepleted()
    {
        IsPowerOut = true;
        PowerLevel = 0f;
        // invoke public event so designer can implement disabling all powergroups
        onPowerOut?.Invoke();
        Debug.Log("[ElectricityLogic] Power depleted - onPowerOut invoked.");
    }

    public void RegisterBreakerButton(BreakerButton button)
    {
        if (button == null) return;
        if (!registeredBreakers.Contains(button))
            registeredBreakers.Add(button);
    }

    public void UnregisterBreakerButton(BreakerButton button)
    {
        if (button == null) return;
        registeredBreakers.Remove(button);
    }

    // Finds all BreakerButtons in the scene and registers them.
    public void RegisterAllInScene()
    {
        var found = FindObjectsByType<BreakerButton>(FindObjectsSortMode.None);
        registeredBreakers.Clear();
        for (int i = 0; i < found.Length; i++)
            registeredBreakers.Add(found[i]);
    }

    // Public reset method requested by designer - will restore power and invoke an event.
    // Designer will hook their ResetBreakerBox implementation to restore breaker states as needed.
    public void ResetBreakerBox()
    {
        PowerLevel = 100f;
        IsPowerOut = false;
        powerAlmostUpSoundPlayed = false; // Reset sound flag when power is restored
        onPowerRestored?.Invoke();
        Debug.Log("[ElectricityLogic] ResetBreakerBox called - power restored.");
    }

    /// <summary>
    /// Force the system into "power out" state immediately and invoke onPowerOut.
    /// Designers can call this when a breaker button or other mechanic should cut global power.
    /// </summary>
    public void ForcePowerOut()
    {
        if (IsPowerOut) return;
        PowerLevel = 0f;
        IsPowerOut = true;
        onPowerOut?.Invoke();
        Debug.Log("[ElectricityLogic] ForcePowerOut called - power forced to 0%.");
    }

    /// <summary>
    /// Set the power level (0..100). This will update IsPowerOut and invoke onPowerOut / onPowerRestored
    /// appropriately when crossing the 0 threshold.
    /// </summary>
    public void SetPowerLevelPercent(float percent)
    {
        float prev = PowerLevel;
        bool wasOut = IsPowerOut;

        PowerLevel = Mathf.Clamp(percent, 0f, 100f);
        IsPowerOut = PowerLevel <= 0f;

        if (IsPowerOut && !wasOut)
        {
            onPowerOut?.Invoke();
            Debug.Log("[ElectricityLogic] SetPowerLevelPercent -> power depleted -> onPowerOut invoked.");
        }
        else if (!IsPowerOut && wasOut)
        {
            onPowerRestored?.Invoke();
            Debug.Log("[ElectricityLogic] SetPowerLevelPercent -> power restored -> onPowerRestored invoked.");
        }
    }

    // Optional helper: returns current percentage 0..100
    public float PowerPercent => PowerLevel;

    private void PlayPowerAlmostUpSound()
    {
        if (powerAlmostUpSource != null && powerAlmostUpClip != null)
        {
            powerAlmostUpSource.PlayOneShot(powerAlmostUpClip);
            powerAlmostUpSoundPlayed = true;
            Debug.Log($"[ElectricityLogic] Power almost up sound played at {PowerLevel}% (threshold: {powerAlmostUpThreshold}%)");
        }
    }
}
