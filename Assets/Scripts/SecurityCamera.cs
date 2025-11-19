using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    public enum Group
    {
        A, B, C, D, E, F
    };
    public Group _group;
    public Light camLight;
    private ManualCameraRenderer cam;

    public bool lights = false;
    void Awake()
    {
        Light templight = GetComponentInChildren<Light>();
        if (templight != null)
            camLight = templight;
        cam  = GetComponent<ManualCameraRenderer>();
    }

    public void EnableLight()
    {
        if (!lights)
        {
            lights = true;
            camLight.enabled = true;
            cam.enabled = true;
        }

    }
    public void DisableLight()
    {
        if (lights)
        {
            lights = false;
            camLight.enabled = false;
            cam.enabled = false;
        }
    }
}