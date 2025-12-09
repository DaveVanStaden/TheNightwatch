using System.Collections;
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
        A, B, C, D, E, F
    };
    public Group _group;

    public GameObject[] cameras;
    public GameObject[] icons;

    private void Awake()
    {
        GameObject[] iconList = GameObject.FindGameObjectsWithTag("CamIcon");
        int k = 0;
        for (int i = 0; i < iconList.Length; i++)
        {
            if (iconList[i].GetComponent<CamIcon>()._group.ToString() == _group.ToString())
            {
                icons[k] = iconList[i];
                k++;
                if (k >= 4)
                {
                    k = 0;
                }
            }
        }
    }
    void Start()
    {
        audioSource = GameObject.Find("MonitorGroupBleeps").GetComponent<AudioSource>();
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
            StartCoroutine(StartSetUp());
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
        //Disable everything, just for a moment
        //First disable the camera's views
        GameObject[] cameraViews = GameObject.FindGameObjectsWithTag("CameraView");
        for (int i = 0; i < cameraViews.Length; i++)
        {
            cameraViews[i].GetComponent<RawImage>().enabled = false;
            cameraViews[i].GetComponent<BoxCollider2D>().enabled = false;
            cameraViews[i].GetComponent<CamImage>().titleText.enabled = false;
            cameraViews[i].GetComponent<CamImage>().descText.enabled = false;
        }
        //Then deselect the other buttons
        GameObject[] groupButtons = GameObject.FindGameObjectsWithTag("GroupButton");
        for (int i = 0; i < groupButtons.Length; i++)
        {
            groupButtons[i].GetComponent<CamGroup>().Deselect();
        }


        //Now that everything is deselected, re-enable all the cameras that are part of our group
        for (int i = 0; i < cameras.Length; i++)
        {
            _ = cameras[i].GetComponent<RawImage>().enabled = true;
            _ = cameras[i].GetComponent<BoxCollider2D>().enabled = true;
            _ = cameras[i].GetComponent<CamImage>().titleText.enabled = true;
            _ = cameras[i].GetComponent<CamImage>().descText.enabled = true;
        }

        MakeSelected();
        ReplaceLights();
        ReplaceIcons();
    }
    private void ReplaceLights()
    {
        //Improve performance by turning off the lights when the camera attatched to those lights is not looking
        GameObject[] allLights = GameObject.FindGameObjectsWithTag("SecurityCam");
        for (int i = 0; i < allLights.Length; i++)
        {
            if (allLights[i].GetComponent<SecurityCamera>().lights)
                allLights[i].GetComponent<SecurityCamera>().DisableLight();
        }
        for (int i = 0; i < cameras.Length; i++)
        {
            if(cameras[i].GetComponent<CamImage>().originalCam != null)
                if (!cameras[i].GetComponent<CamImage>().originalCam.lights)
                    cameras[i].GetComponent<CamImage>().originalCam.EnableLight();
        }


    }
    private void ReplaceIcons()
    {
        //Deselect all camIcons
        GameObject[] allIcons = GameObject.FindGameObjectsWithTag("CamIcon");
        for (int i = 0; i < allIcons.Length; i++)
        {
            allIcons[i].GetComponent<CamIcon>().Deselect();
        }
        //Then re-enable the icons that are part of the group
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].GetComponent<CamIcon>().Select();
        }
    }

    IEnumerator StartSetUp()
    {
        yield return new WaitForSeconds(.2f);
        ReplaceCameras();
        FindAnyObjectByType<DisableCams>().SwitchEm();
    }
}