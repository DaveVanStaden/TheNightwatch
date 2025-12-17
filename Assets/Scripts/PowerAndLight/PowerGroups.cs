using System.Collections.Generic;
using UnityEngine;

public class PowerGroups : MonoBehaviour
{
    [SerializeField] private List<GameObject> Lights = new List<GameObject>();

    private ElectricityLogic elec;

    private void OnEnable()
    {
        // Subscribe to global power events so groups can immediately turn off when global power is out.
        elec = Object.FindAnyObjectByType<ElectricityLogic>();
        if (elec != null)
        {
            elec.onPowerOut.AddListener(OnGlobalPowerOut);
            // When power restored we don't force-turn-on here; breakers re-apply their own state.
            elec.onPowerRestored.AddListener(OnGlobalPowerRestored);
        }
    }

    private void OnDisable()
    {
        if (elec != null)
        {
            elec.onPowerOut.RemoveListener(OnGlobalPowerOut);
            elec.onPowerRestored.RemoveListener(OnGlobalPowerRestored);
        }
    }

    private void OnGlobalPowerOut()
    {
        // Global power is out: ensure group is off immediately
        SetLights(false);
    }

    private void OnGlobalPowerRestored()
    {
        // When global power is restored we'll let breakers decide whether to turn this group on.
        // No action here to avoid overriding breakers.
        Debug.Log($"[PowerGroups] Global power restored for group '{name}'. Breakers will reapply state.");
    }

    public void TurnOnLights()
    {
        // Only turn on if global power available
        if (Object.FindAnyObjectByType<ElectricityLogic>() is ElectricityLogic e && e.IsPowerOut)
            return;

        SetLights(true);
    }

    public void TurnOffLights()
    {
        SetLights(false);
    }

    public void SetLights(bool on)
    {
        foreach (GameObject light in Lights)
        {
            if (light == null) continue;
            var lt = light.GetComponent<Light>();
            if (lt != null)
                lt.enabled = on;

            light.SetActive(on);

            // Debug: log which gameobjects are being changed so we can trace unexpected toggles
            Debug.Log($"[PowerGroups] Group '{name}' SetLights({on}) on object '{light.name}' (active now = {light.activeSelf})");
        }
    }

    public bool AnyLightOn()
    {
        foreach (GameObject light in Lights)
        {
            if (light == null) continue;
            var lt = light.GetComponent<Light>();
            if (lt != null)
            {
                if (lt.enabled) return true;
            }
            else
            {
                if (light.activeSelf) return true;
            }
        }
        return false;
    }
}