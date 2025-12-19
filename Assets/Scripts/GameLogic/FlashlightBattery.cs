using System.Collections.Generic;
using UnityEngine;

public class FlashlightBattery : MonoBehaviour
{
    public static FlashlightBattery Instance { get; private set; }

    [Header("Battery")]
    [Tooltip("Battery normalized 0..1 (readonly at runtime)")]
    [SerializeField, Range(0f, 1f)] private float battery = 1f;
    [Tooltip("Seconds of full-on drain from 100% -> 0% when ALL lights run at their configured maxIntensity")]
    [SerializeField] private float batteryDurationSeconds = 300f;
    [Tooltip("Start softening intensity below this normalized battery (0..1)")]
    [SerializeField, Range(0f, 1f)] private float softThreshold = 0.7f;

    [Header("Toggle audio clips")]
    [Tooltip("Played when main flashlight toggles while battery > 0")]
    [SerializeField] private AudioClip toggleClipNormal;
    [Tooltip("Played when main flashlight toggles while battery == 0")]
    [SerializeField] private AudioClip toggleClipEmpty;

    [Header("Interaction / BatteryBox")]
    [Tooltip("Max distance to interact with the battery box via raycast (duplicate of PlayerInteraction range tolerance)")]
    [SerializeField] private float batteryBoxInteractRange = 3f;
    [Tooltip("Optional: sound played from the battery box audio source when recharging.")]
    [SerializeField] private AudioClip batteryBoxRechargeClip;

    // Internal
    private List<Flashlight> allFlashlights = new List<Flashlight>();
    private Flashlight mainFlashlight;
    private AudioSource sharedAudioSource; // plays toggle clips when main flashlight exists
    private PlayerManager playerManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // Find all Flashlight instances in the scene and identify main
        var found = FindObjectsOfType<Flashlight>();
        allFlashlights = new List<Flashlight>(found.Length);
        foreach (var f in found)
        {
            if (f == null) continue;
            allFlashlights.Add(f);
            if (f.mainLight)
                mainFlashlight = f;
        }

        // Create/attach a shared audio source on this manager if main doesn't have one
        sharedAudioSource = GetComponent<AudioSource>();
        if (sharedAudioSource == null)
            sharedAudioSource = gameObject.AddComponent<AudioSource>();

        // Use main audio source if available on main flashlight
        if (mainFlashlight != null)
        {
            var mainAS = mainFlashlight.GetComponent<AudioSource>();
            if (mainAS != null)
                sharedAudioSource = mainAS;
        }

        // locate playerManager for batterybox raycast interactions
        playerManager = Object.FindAnyObjectByType<PlayerManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // refresh list if scene changed (robustness)
        if (allFlashlights == null || allFlashlights.Count == 0)
        {
            var found = FindObjectsOfType<Flashlight>();
            allFlashlights = new List<Flashlight>(found.Length);
            foreach (var f in found) if (f != null) allFlashlights.Add(f);
            // re-find main if needed
            if (mainFlashlight == null)
                mainFlashlight = allFlashlights.Find(x => x != null && x.mainLight);
        }

        // Compute total configured power (sum of maxIntensity) and current used power
        float totalMaxPower = 0f;
        float currentUsedPower = 0f;
        foreach (var f in allFlashlights)
        {
            if (f == null) continue;
            // use configured maxIntensity as the "capacity" of that light
            totalMaxPower += Mathf.Max(0f, f.maxIntensity);
            if (f.onOrOff)
                currentUsedPower += Mathf.Max(0f, f.maxIntensity);
        }

        // Drain only when the main flashlight is on (per design) and battery > 0
        if (mainFlashlight != null && mainFlashlight.onOrOff && battery > 0f)
        {
            // If totalMaxPower is zero fallback to 1 to avoid division by zero
            float usageFraction = totalMaxPower > 0f ? (currentUsedPower / totalMaxPower) : 1f;

            // Drain proportional to usage fraction. batteryDurationSeconds describes how long
            // it would take to drain from 1->0 if usageFraction == 1 (all lights at max).
            if (batteryDurationSeconds > 0f && usageFraction > 0f)
            {
                float drainThisFrame = (usageFraction / batteryDurationSeconds) * Time.deltaTime;
                battery = Mathf.Clamp01(battery - drainThisFrame);
            }
        }

        // compute intensity factor based on softThreshold
        float intensityFactor = ComputeIntensityFactor();

        // apply intensities to all flashlights based on their on/off state and battery
        foreach (var f in allFlashlights)
        {
            if (f == null) continue;

            // ensure the light reference is resolved (Flashlight script caches it in Start but be robust)
            var light = f.GetComponent<Light>();
            if (light == null) continue;

            // Update the per-flashlight stored intensity first (do not overwrite inspector maxIntensity)
            // This keeps each light's configured base value but adjusts the stored "current" value.
            f.currentMaxIntensity = f.maxIntensity * intensityFactor;

            if (battery <= 0f)
            {
                // no battery -> no light even when toggled on
                light.intensity = 0f;
            }
            else
            {
                if (f.onOrOff)
                    // now use the stored current value (reflects per-light configured maxIntensity scaled by battery)
                    light.intensity = f.currentMaxIntensity;
                else
                    light.intensity = 0f;
            }
        }

        // handle batterybox simple interact (player pressed Interact while looking at a battery box)
        if (playerManager != null && playerManager.inputActions != null)
        {
            if (playerManager.inputActions.Player.Interact.triggered)
            {
                TryRechargeFromLookingBatteryBox();
            }
        }
        else
        {
            // fallback to legacy Input for projects not using the Input System
            if (Input.GetMouseButtonDown(0))
            {
                TryRechargeFromLookingBatteryBox();
            }
        }
    }

    // Public read-only battery value
    public float BatteryNormalized => battery;

    public float SoftThreshold => softThreshold;

    /// <summary>
    /// Compute 0..1 intensity multiplier based on battery and softThreshold.
    /// Above softThreshold => 1. Below it scales linearly to 0 at battery==0.
    /// </summary>
    public float ComputeIntensityFactor()
    {
        if (battery <= 0f) return 0f;
        if (battery >= softThreshold) return 1f;
        // scale from 0..softThreshold -> 0..1
        return Mathf.Clamp01(battery / Mathf.Max(0.0001f, softThreshold));
    }

    /// <summary>
    /// Called by Flashlight (main) to play the appropriate toggle sound.
    /// </summary>
    public void PlayToggleSoundForMain()
    {
        if (sharedAudioSource == null) return;

        AudioClip clipToPlay = (battery <= 0f) ? toggleClipEmpty : toggleClipNormal;
        if (clipToPlay == null) return;
        sharedAudioSource.pitch = Random.Range(0.98f, 1.02f);
        sharedAudioSource.PlayOneShot(clipToPlay);
    }

    /// <summary>
    /// Force a full recharge (set battery to 1).
    /// Plays the provided battery box clip on the battery box audio source (if available).
    /// </summary>
    public void Recharge(float setTo = 1f, AudioSource batteryBoxSource = null, AudioClip boxClip = null)
    {
        battery = Mathf.Clamp01(setTo);
        // update stored intensities immediately (next Update will also run)
        float factor = ComputeIntensityFactor();
        foreach (var f in allFlashlights)
        {
            if (f == null) continue;
            var light = f.GetComponent<Light>();
            if (light == null) continue;

            // update stored/current intensity
            f.currentMaxIntensity = f.maxIntensity * factor;

            if (battery <= 0f) light.intensity = 0f;
            else light.intensity = f.onOrOff ? f.currentMaxIntensity : 0f;
        }

        // Play recharge sound on the batterybox's audio source if provided, otherwise play here
        AudioClip clip = boxClip != null ? boxClip : batteryBoxRechargeClip;
        if (batteryBoxSource != null && clip != null)
        {
            batteryBoxSource.pitch = Random.Range(0.98f, 1.02f);
            batteryBoxSource.PlayOneShot(clip);
        }
        else if (clip != null && sharedAudioSource != null)
        {
            sharedAudioSource.PlayOneShot(clip);
        }
    }

    // Lightweight raycast from player camera to detect BatteryBox (tag) and recharge it.
    // This allows recharging even if BatteryBox isn't an IInteraction.
    private void TryRechargeFromLookingBatteryBox()
    {
        Camera cam = playerManager != null ? playerManager.playerCamera : Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, batteryBoxInteractRange, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider != null && hit.collider.CompareTag("BatteryBox"))
            {
                // Try to find an AudioSource on the hit object to play recharge clip
                var boxAS = hit.collider.GetComponentInParent<AudioSource>() ?? hit.collider.GetComponent<AudioSource>();
                Recharge(1f, boxAS, batteryBoxRechargeClip);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        battery = Mathf.Clamp01(battery);
        batteryDurationSeconds = Mathf.Max(0.01f, batteryDurationSeconds);
        softThreshold = Mathf.Clamp01(softThreshold);
    }
#endif
}
