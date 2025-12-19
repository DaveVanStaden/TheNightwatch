using UnityEngine;

public class DisableCams : MonoBehaviour
{
    bool AreCamsOn = true;
    private SecurityCamera[] cams;
    [SerializeField] ManualCameraRenderer[] additionalRenderers;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cams = FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Cams must be off, so it's set to false to make sure it switches them correctly
            AreCamsOn = false;
            SwitchEm();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Cams must be on, so it's set to true to make sure it switches them correctly
            AreCamsOn = true;
            SwitchEm();
        }
    }

    public void SwitchEm()
    {
        if (AreCamsOn)
        {
            //Cams are on, so turn them off
            foreach (SecurityCamera cam in cams)
            {
                cam.GetComponent<ManualCameraRenderer>().enabled = false;

                if (cam.camLight != null)
                {
                    cam.camLight.enabled = false;
                }
            }
            if (additionalRenderers != null)
            {
                foreach (ManualCameraRenderer cam in additionalRenderers)
                {
                    cam.GetComponent<ManualCameraRenderer>().enabled = false;
                }
            }
        }
        else
        {
            //Cams are off, so turn them on
            foreach (SecurityCamera cam in cams)
            {
                if (cam.lights)
                {
                    cam.GetComponent<ManualCameraRenderer>().enabled = true;
                    if (cam.camLight != null)
                    {
                        cam.camLight.enabled = true;
                    }
                }
            }
            if (additionalRenderers != null)
            {
                foreach (ManualCameraRenderer cam in additionalRenderers)
                {
                    cam.GetComponent<ManualCameraRenderer>().enabled = true;
                }
            }
        }
    }

}
