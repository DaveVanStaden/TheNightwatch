using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    public enum Group
    {
        A, B, C, D
    };
    public Group _group;
    public Light camLight;

    public bool lights = false;
    void Awake()
    {
        Light templight = GetComponentInChildren<Light>();
        if (templight != null)
            camLight = templight;
    }

    public void EnableLight()
    {
        if (!lights)
        {
            lights = true;
            camLight.enabled = true;
        }

    }
    public void DisableLight()
    {
        if (lights)
        {
            lights = false;
            camLight.enabled = false;
        }
    }
}