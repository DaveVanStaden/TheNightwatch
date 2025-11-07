using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CamGroup : MonoBehaviour
{
    public bool isStartingGroup = false;
    public bool selectedGroup = false;
    public Color regularColor = Color.white;
    public Color selectedColor;
    public Color hoverColor;
    public Color currentColor;

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip blip;
    public enum Group
    {
        A, B, C, D
    };
    public Group _group;

    public GameObject[] cameras;
    void Start()
    {
        GameObject[] cameralist = GameObject.FindGameObjectsWithTag("CameraView");
        int j = 0;
        for (int i = 0; i < cameralist.Length; i++)
        {
            if (cameralist[i].GetComponent<CamImage>()._group.ToString() == _group.ToString())
            {
                cameras[j] = cameralist[i];
                j++;
                if (j >= 4)
                {
                    j = 0;
                }
            }
        }
        currentColor = regularColor;
        if (isStartingGroup)
        {
            ReplaceCameras();
        }
    }

    private void Update()
    {
        GetComponent<Image>().color = currentColor;
    }

    private void MakeSelected()
    {
        currentColor = selectedColor;
        selectedGroup = true;
    }
    public void Deselect()
    {
        currentColor = regularColor;
        selectedGroup = false;
    }
    public void ReplaceCameras()
    {
        audioSource.PlayOneShot(blip);
        GameObject[] cameraViews = GameObject.FindGameObjectsWithTag("CameraView");
        for (int i = 0; i < cameraViews.Length; i++)
        {
            cameraViews[i].GetComponent<RawImage>().enabled = false;
            cameraViews[i].GetComponent<BoxCollider2D>().enabled = false;
            cameraViews[i].GetComponent<CamImage>().titleText.enabled = false;
            cameraViews[i].GetComponent<CamImage>().descText.enabled = false;
        }
        GameObject[] groupButtons = GameObject.FindGameObjectsWithTag("GroupButton");
        for (int i = 0; i < groupButtons.Length; i++)
        {
            groupButtons[i].GetComponent<CamGroup>().Deselect();
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            _ = cameras[i].GetComponent<RawImage>().enabled = true;
            _ = cameras[i].GetComponent<BoxCollider2D>().enabled = true;
            _ = cameras[i].GetComponent<CamImage>().titleText.enabled = true;
            _ = cameras[i].GetComponent<CamImage>().descText.enabled = true;
        }
        MakeSelected();
        ReplaceLights();
    }
    private void ReplaceLights()
    {
        //work in progress code, intended for performance improvements
        GameObject[] allLights = GameObject.FindGameObjectsWithTag("SecurityCam");
        for (int i = 0; i < allLights.Length; i++)
        {
            if (allLights[i].GetComponent<SecurityCamera>().lights)
                allLights[i].GetComponent<SecurityCamera>().DisableLight();
        }
        for (int i = 0; i < cameras.Length; i++)
        {
            if (!cameras[i].GetComponent<CamImage>().originalCam.lights)
                cameras[i].GetComponent<CamImage>().originalCam.EnableLight();
        }


    }

}
