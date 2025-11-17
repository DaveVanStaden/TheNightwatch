using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [SerializeField] private Light flashlight;
    PlayerManager player;
    public bool onOrOff = false;
    public float maxIntensity;
    public bool mainLight;
    AudioSource click;

    // Controls whether player input can toggle the flashlight
    private bool inputEnabled = true;

    private void Start()
    {
        click = GetComponent<AudioSource>();
        flashlight = GetComponent<Light>();
        player = FindAnyObjectByType<PlayerManager>();
    }

    void Update()
    {
        // Don't let the player toggle while input is disabled
        if (!inputEnabled)
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

        if (mainLight)
        {
            PlaySound();
        }
        if (onOrOff)
        {
            flashlight.intensity = 0f;
            onOrOff = false;
        }
        else
        {
            flashlight.intensity = maxIntensity;
            onOrOff = true;
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

        if (on)
        {
            flashlight.intensity = maxIntensity;
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
