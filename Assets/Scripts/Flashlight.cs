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

    private void Start()
    {
        click = GetComponent<AudioSource>();
        flashlight = GetComponent<Light>();
        player = FindObjectOfType<PlayerManager>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SwitchLight();
            //Debug.Log("wawawa");

        }
    }

    public void SwitchLight()
    {
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
        click.pitch = Random.Range(0.98f, 1.02f);
        click.PlayOneShot(click.clip);
    }
}
