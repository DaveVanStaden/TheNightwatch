using UnityEngine;

public class DisableCams : MonoBehaviour
{
    bool AreCamsOn = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

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
            foreach (GameObject cam in GameObject.FindGameObjectsWithTag("SecurityCam"))
            {
                cam.GetComponent<ManualCameraRenderer>().fps = .1f;
                if (cam.GetComponentInChildren<Light>() != null)
                {
                    cam.GetComponentInChildren<Light>().enabled = false;
                }
            }
        }
        else
        {
            //Cams are off, so turn them on
            foreach (GameObject cam in GameObject.FindGameObjectsWithTag("SecurityCam"))
            {
                cam.GetComponent<ManualCameraRenderer>().fps = 12f;
                if (cam.GetComponentInChildren<Light>() != null)
                {
                    cam.GetComponentInChildren<Light>().enabled = true;
                }
            }
        }
    }

}
