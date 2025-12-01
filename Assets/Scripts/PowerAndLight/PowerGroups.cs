using System.Collections.Generic;
using UnityEngine;

public class PowerGroups : MonoBehaviour
{
    [SerializeField] private List<GameObject> Lights = new List<GameObject>();

    public void TurnOnLights()
    {
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