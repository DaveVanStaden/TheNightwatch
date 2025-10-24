using UnityEngine;

public class DisableCams : MonoBehaviour
{
    bool AreCamsOn = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SwitchEm();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Entering cams range");
            // Cams must be off, so it's set to false to make sure it switches them correctly
            AreCamsOn = false;
            SwitchEm();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Exiting cams range");
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
            foreach (SecurityCamera cam in FindObjectsOfType<SecurityCamera>())
            {
                cam.GetComponent<ManualCameraRenderer>().fps = .1f;
                if (cam.camLight != null)
                {
                    cam.camLight.enabled = false;
                }
            }
        }
        else
        {
            //Cams are off, so turn them on
            foreach (SecurityCamera cam in FindObjectsOfType<SecurityCamera>())
            {
                cam.GetComponent<ManualCameraRenderer>().fps = 12f;
                if (cam.camLight != null)
                {
                    if (cam.lights)
                        cam.camLight.enabled = true;
                }
            }
        }
    }

}
