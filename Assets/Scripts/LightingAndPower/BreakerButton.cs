using UnityEngine;

public class BreakerButton : MonoBehaviour
{
    [SerializeField] private PowerGroups powerGroup;
    [SerializeField] private bool startsOn = true;
    [SerializeField] private AudioSource clickAudio;
    public bool isOn { get; private set; }

    private void Start()
    {
        isOn = startsOn;
        ApplyState();
    }

    public void Toggle()
    {
        isOn = !isOn;
        ApplyState();
        if (clickAudio != null)
            clickAudio.Play();
    }

    private void ApplyState()
    {
        if (powerGroup == null) return;
        if (isOn)
            powerGroup.TurnOnLights();
        else
            powerGroup.TurnOffLights();
    }
}
