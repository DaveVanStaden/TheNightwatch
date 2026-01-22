using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LightSwitch : MonoBehaviour
{
    [Header("Lights")]
    [Tooltip("List of lights controlled by this switch")]
    [SerializeField] private GameObject[] lights;

    [Header("Sanity Settings")]
    [Tooltip("Sanity gained immediately when lights are turned on")]
    [SerializeField] private float initialSanityBoost = 10f;

    [Tooltip("Sanity gained per second while lights remain on")]
    [SerializeField] private float sanityPerSecond = 2f;

    [Tooltip("Cooldown in seconds before instant sanity boost can be gained again (counts down while lights OFF)")]
    [SerializeField] private float sanityBoostCooldown = 120f;

    [Header("Breaking Settings")]
    [Tooltip("Initial chance per check for lights to break (0-1)")]
    [SerializeField] private float initialBreakChance = 0.0001f;

    [Tooltip("How much the break chance increases per second")]
    [SerializeField] private float breakChanceIncreasePerSecond = 0.0001f;

    [Tooltip("Maximum break chance (0-1)")]
    [SerializeField] private float maxBreakChance = 0.01f;

    [Tooltip("How often to check if lights should break (in seconds)")]
    [SerializeField] private float breakCheckInterval = 1f;

    [Tooltip("Maximum time before lights are guaranteed to break (in seconds)")]
    [SerializeField] private float maxTimeBeforeBreak = 300f;

    [Header("Breaker Connection")]
    [Tooltip("Optional breaker button that will turn off when lights break")]
    [SerializeField] private BreakerButton connectedFuse;

    [Header("Audio")]
    [Tooltip("Audio source for playing switch sounds")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played when turning lights on")]
    [SerializeField] private AudioClip switchOnClip;

    [Tooltip("Sound played when turning lights off")]
    [SerializeField] private AudioClip switchOffClip;

    [Tooltip("Sound played when lights break")]
    [SerializeField] private AudioClip breakClip;

    private bool lightsOn = false;
    private bool isBroken = false;
    private float currentBreakChance;
    private float timeOn = 0f;
    private Coroutine sanityCoroutine;
    private Coroutine breakCheckCoroutine;

    // Track if lights were on before power loss
    private bool wereOnBeforePowerLoss = false;

    // Sanity boost cooldown tracking
    private float sanityBoostTimer = 0f;

    // PlayerStats reflection (kept for cross-assembly compatibility)
    private System.Reflection.MethodInfo changeSanityMethod;
    private System.Type playerStatsType;

    // Shadow control
    private HallucinationSpawner shadowSpawner;
    private bool shadowsDisabledByThisSwitch = false;

    private void Start()
    {
        // Lights should ALWAYS start OFF, regardless of breaker state
        // They must be manually toggled ON by the player
        lightsOn = false;
        
        if (lights != null)
        {
            foreach (var light in lights)
            {
                if (light != null)
                {
                    light.SetActive(false);
                }
            }
        }

        currentBreakChance = initialBreakChance;

        if (connectedFuse != null)
        {
            //Debug.Log($"[LightSwitch] '{gameObject.name}' has connected fuse: '{connectedFuse.name}'");
           // Debug.Log($"[LightSwitch] Connected fuse initial state: isOn={connectedFuse.GetState()}");
        }
        else
        {
            Debug.Log($"[LightSwitch] '{gameObject.name}' has no connected fuse assigned");
        }

        playerStatsType = System.Type.GetType("PlayerStats");
        if (playerStatsType != null)
        {
            changeSanityMethod = playerStatsType.GetMethod("ChangeSanity");
        }

        // Find shadow spawner
        shadowSpawner = Object.FindFirstObjectByType<HallucinationSpawner>();
        if (shadowSpawner != null)
        {
            //Debug.Log($"[LightSwitch] '{gameObject.name}' found HallucinationSpawner");
        }
        else
        {
            Debug.LogWarning($"[LightSwitch] '{gameObject.name}' could not find HallucinationSpawner");
        }
    }

    private void Update()
    {
        if (connectedFuse == null) return;

        bool fuseIsOn = connectedFuse.GetState();

        // Scenario 1: Lights are ON and fuse turns OFF -> Force lights off
        if (lightsOn && !fuseIsOn)
        {
            Debug.Log($"[LightSwitch] Connected fuse turned off - forcing lights OFF on {gameObject.name}");
            wereOnBeforePowerLoss = true; // Remember lights were on so they can be manually turned on again
            ForceOffDueToPowerLoss();
        }
        // Lights will NOT automatically restore when power comes back
        // Player must manually toggle the switch ON again

        // Count down sanity boost cooldown timer while lights are OFF
        if (!lightsOn && sanityBoostTimer > 0f)
        {
            sanityBoostTimer -= Time.deltaTime;
            if (sanityBoostTimer < 0f)
            {
                sanityBoostTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Force lights off when power is lost (fuse turned off)
    /// </summary>
    private void ForceOffDueToPowerLoss()
    {
        lightsOn = false;

        // Turn off all lights
        if (lights != null)
        {
            foreach (var light in lights)
            {
                if (light != null)
                {
                    light.SetActive(false);
                }
            }
        }

        // Stop sanity gain
        if (sanityCoroutine != null)
        {
            StopCoroutine(sanityCoroutine);
            sanityCoroutine = null;
        }

        // Stop break checking
        if (breakCheckCoroutine != null)
        {
            StopCoroutine(breakCheckCoroutine);
            breakCheckCoroutine = null;
        }

        // Reset break chance and time
        timeOn = 0f;
        currentBreakChance = initialBreakChance;

        // Re-enable shadow spawning
        if (shadowSpawner != null && shadowsDisabledByThisSwitch)
        {
            shadowSpawner.SetSpawningEnabled(true);
            shadowsDisabledByThisSwitch = false;
            Debug.Log($"[LightSwitch] {gameObject.name} re-enabled shadow spawning (power lost)");
        }

        // Play off sound for feedback
        PlaySound(switchOffClip);
    }

    public void Toggle()
    {
        Debug.Log($"[LightSwitch] Toggle called on {gameObject.name}. Current state: lightsOn={lightsOn}, isBroken={isBroken}");
        
        // Check if the connected fuse is on
        bool fuseIsOn = connectedFuse != null && connectedFuse.GetState();
        
        // IMPORTANT: Cannot turn lights ON if the fuse/breaker is OFF
        if (!lightsOn && connectedFuse != null && !fuseIsOn)
        {
            Debug.Log($"[LightSwitch] Cannot turn lights on - connected fuse '{connectedFuse.name}' is OFF on {gameObject.name}");
            PlaySound(switchOffClip); // Play feedback sound to indicate failed attempt
            return;
        }
        
        // Check if lights are broken and fuse is off
        if (isBroken && !fuseIsOn)
        {
            Debug.Log($"[LightSwitch] Cannot toggle - lights are broken and fuse is off on {gameObject.name}");
            return;
        }

        // If fuse is on and lights were broken, reset the broken state
        if (isBroken && fuseIsOn)
        {
            Debug.Log($"[LightSwitch] Fuse is on - resetting broken state on {gameObject.name}");
            isBroken = false;
            currentBreakChance = initialBreakChance;
            timeOn = 0f;
        }

        lightsOn = !lightsOn;
        Debug.Log($"[LightSwitch] {gameObject.name} lights now: {(lightsOn ? "ON" : "OFF")}");

        if (lights != null)
        {
            foreach (var light in lights)
            {
                if (light != null)
                {
                    light.SetActive(lightsOn);
                }
            }
        }

        if (lightsOn)
        {
            // Clear power loss flag since lights are now on
            wereOnBeforePowerLoss = false;
            
            // Only grant instant sanity boost if cooldown has expired
            if (sanityBoostTimer <= 0f)
            {
                AddSanity(initialSanityBoost);
                sanityBoostTimer = sanityBoostCooldown; // Reset cooldown
                Debug.Log($"[LightSwitch] {gameObject.name} granted instant sanity boost of {initialSanityBoost}. Cooldown reset to {sanityBoostCooldown}s");
            }
            else
            {
                Debug.Log($"[LightSwitch] {gameObject.name} instant sanity boost on cooldown ({sanityBoostTimer:F1}s remaining)");
            }

            if (sanityCoroutine != null)
            {
                StopCoroutine(sanityCoroutine);
            }
            sanityCoroutine = StartCoroutine(SanityGainCoroutine());

            if (breakCheckCoroutine != null)
            {
                StopCoroutine(breakCheckCoroutine);
            }
            breakCheckCoroutine = StartCoroutine(BreakCheckCoroutine());

            // Disable shadow spawning and destroy current shadow when lights turn on
            if (shadowSpawner != null)
            {
                shadowSpawner.DestroyCurrentShadow();
                shadowSpawner.SetSpawningEnabled(false);
                shadowsDisabledByThisSwitch = true;
                Debug.Log($"[LightSwitch] {gameObject.name} disabled shadow spawning (lights ON)");
            }

            PlaySound(switchOnClip);
        }
        else
        {
            if (sanityCoroutine != null)
            {
                StopCoroutine(sanityCoroutine);
                sanityCoroutine = null;
            }

            if (breakCheckCoroutine != null)
            {
                StopCoroutine(breakCheckCoroutine);
                breakCheckCoroutine = null;
            }

            // IMPORTANT: Reset break chance and time when lights are manually turned off
            // This prevents the break chance from accumulating when toggling lights
            timeOn = 0f;
            currentBreakChance = initialBreakChance;
            Debug.Log($"[LightSwitch] Break chance reset to {initialBreakChance} on {gameObject.name}");

            // Clear the power loss flag since lights are being manually turned off
            wereOnBeforePowerLoss = false;

            // Re-enable shadow spawning when lights turn off
            if (shadowSpawner != null && shadowsDisabledByThisSwitch)
            {
                shadowSpawner.SetSpawningEnabled(true);
                shadowsDisabledByThisSwitch = false;
                Debug.Log($"[LightSwitch] {gameObject.name} re-enabled shadow spawning (lights OFF)");
            }

            PlaySound(switchOffClip);
        }
    }

    private void AddSanity(float amount)
    {
        if (playerStatsType == null || changeSanityMethod == null) return;

        var instanceProperty = playerStatsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (instanceProperty != null)
        {
            var playerStatsInstance = instanceProperty.GetValue(null, null);
            if (playerStatsInstance != null)
            {
                changeSanityMethod.Invoke(playerStatsInstance, new object[] { amount });
            }
        }
    }

    private IEnumerator SanityGainCoroutine()
    {
        while (lightsOn && !isBroken)
        {
            yield return new WaitForSeconds(1f);
            AddSanity(sanityPerSecond);
        }
    }

    private IEnumerator BreakCheckCoroutine()
    {
        while (lightsOn && !isBroken)
        {
            yield return new WaitForSeconds(breakCheckInterval);
            
            timeOn += breakCheckInterval;

            if (timeOn >= maxTimeBeforeBreak)
            {
                BreakLights();
                yield break;
            }

            currentBreakChance = Mathf.Min(currentBreakChance + (breakChanceIncreasePerSecond * breakCheckInterval), maxBreakChance);

            if (Random.value < currentBreakChance)
            {
                BreakLights();
                yield break;
            }
        }
    }

    private void BreakLights()
    {
        Debug.Log($"[LightSwitch] Lights breaking on {gameObject.name}");
        
        isBroken = true;
        lightsOn = false;

        if (lights != null)
        {
            foreach (var light in lights)
            {
                if (light != null)
                {
                    light.SetActive(false);
                }
            }
        }

        if (sanityCoroutine != null)
        {
            StopCoroutine(sanityCoroutine);
            sanityCoroutine = null;
        }

        if (breakCheckCoroutine != null)
        {
            StopCoroutine(breakCheckCoroutine);
            breakCheckCoroutine = null;
        }

        // Reset break chance and time when lights break
        timeOn = 0f;
        currentBreakChance = initialBreakChance;

        // Turn off the connected fuse using Toggle to trigger the animation
        if (connectedFuse != null)
        {
            Debug.Log($"[LightSwitch] Attempting to toggle connected fuse '{connectedFuse.name}'");
            Debug.Log($"[LightSwitch] Fuse current state before Toggle: {connectedFuse.GetState()}");
            
            // Only toggle if the fuse is currently ON (we want to turn it OFF)
            if (connectedFuse.GetState())
            {
                connectedFuse.Toggle();
                Debug.Log($"[LightSwitch] Fuse toggled. New state: {connectedFuse.GetState()}");
            }
            else
            {
                Debug.Log($"[LightSwitch] Fuse is already off, no need to toggle");
            }
        }
        else
        {
            Debug.Log($"[LightSwitch] No connected fuse assigned on {gameObject.name}");
        }

        PlaySound(breakClip);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void OnDestroy()
    {
        if (sanityCoroutine != null)
        {
            StopCoroutine(sanityCoroutine);
        }

        if (breakCheckCoroutine != null)
        {
            StopCoroutine(breakCheckCoroutine);
        }

        // Re-enable shadow spawning if this switch disabled it and is being destroyed
        if (shadowSpawner != null && shadowsDisabledByThisSwitch)
        {
            shadowSpawner.SetSpawningEnabled(true);
            Debug.Log($"[LightSwitch] {gameObject.name} re-enabled shadow spawning (destroyed)");
        }
    }
}
