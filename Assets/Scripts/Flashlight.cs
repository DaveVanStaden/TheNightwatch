using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private Light flashlight;
    PlayerManager player;
    public bool onOrOff = false;
    public float maxIntensity;
    // Effective intensity that battery system may modify (per-light stored value)
    public float currentMaxIntensity;
    public bool mainLight;
    AudioSource click;

    // Controls whether player input can toggle the flashlight
    private bool inputEnabled = true;

    private void Start()
    {
        click = GetComponent<AudioSource>();
        flashlight = GetComponent<Light>();
        player = FindAnyObjectByType<PlayerManager>();

        // initialize stored/current intensity from inspector maxIntensity
        currentMaxIntensity = maxIntensity;

        // apply initial intensity according to on/off and battery (battery manager may override next frame)
        var bm = FlashlightBattery.Instance;
        float factor = (bm != null) ? bm.ComputeIntensityFactor() : 1f;
        if (onOrOff)
            flashlight.intensity = (bm != null && bm.BatteryNormalized <= 0f) ? 0f : maxIntensity * factor;
        else
            flashlight.intensity = 0f;
    }

    void Update()
    {
        // Don't let the player toggle while input is disabled
        if (!inputEnabled)
            return;
        // Don't let the player toggle while time is paused
        if (Time.timeScale != 1)
            return;
        if (Input.GetMouseButtonDown(0))
        {
            SwitchLight();
        }
    }

    public void SwitchLight()
    {
        // Prevent toggles when disabled
        if (!inputEnabled)
            return;

        // For main flashlight toggles delegate sound selection to FlashlightBattery so it can choose
        // normal vs empty clip based on battery
        if (mainLight)
        {
            var batteryMgr = FlashlightBattery.Instance;
            if (batteryMgr != null)
            {
                batteryMgr.PlayToggleSoundForMain();
            }
            else
            {
                // fallback to local click sound if battery manager missing
                PlaySound();
            }
        }
        else
        {
            // non-main lights keep their existing click
            PlaySound();
        }

        // Toggle state
        onOrOff = !onOrOff;

        // Apply immediate intensity respecting current battery / softening
        var bm = FlashlightBattery.Instance;
        float intensityFactor = (bm != null) ? bm.ComputeIntensityFactor() : 1f;

        // Update the stored per-light effective intensity first (user requested)
        currentMaxIntensity = maxIntensity * intensityFactor;

        if (onOrOff)
        {
            // If battery is 0 we still set onOrOff true, but intensity will be set by battery manager (or be zero here)
            flashlight.intensity = (bm != null && bm.BatteryNormalized <= 0f) ? 0f : currentMaxIntensity;
        }
        else
        {
            flashlight.intensity = 0f;
        }
    }

    public void PlaySound()
    {
        if (click == null) click = GetComponent<AudioSource>();
        if (click == null) return;
        click.pitch = Random.Range(0.98f, 1.02f);
        click.PlayOneShot(click.clip);
    }

    // Public API for interactions

    public void DisableInput()
    {
        inputEnabled = false;
    }

    public void EnableInput()
    {
        inputEnabled = true;
    }

    // Force set the flashlight state regardless of inputEnabled
    public void ForceSet(bool on)
    {
        if (flashlight == null)
            flashlight = GetComponent<Light>();

        onOrOff = on;

        var bm = FlashlightBattery.Instance;
        float intensityFactor = (bm != null) ? bm.ComputeIntensityFactor() : 1f;

        // update stored/current intensity before applying
        currentMaxIntensity = maxIntensity * intensityFactor;

        if (on)
        {
            flashlight.intensity = (bm != null && bm.BatteryNormalized <= 0f) ? 0f : currentMaxIntensity;
            onOrOff = true;
        }
        else
        {
            flashlight.intensity = 0f;
            onOrOff = false;
        }
    }

    public void ForceOff()
    {
        ForceSet(false);
    }
}
