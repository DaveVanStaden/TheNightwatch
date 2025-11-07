using System.Collections.Generic;
using UnityEngine;

public class PowerGroups : MonoBehaviour
{
    [SerializeField]private List<GameObject> Lights = new List<GameObject>();
    public void TurnOnLights()
    {
        foreach (GameObject light in Lights)
        {
            light.SetActive(true);
        }
    }
    public void TurnOffLights()
    {
        foreach (GameObject light in Lights)
        {
            light.SetActive(false);
        }
    }
}
