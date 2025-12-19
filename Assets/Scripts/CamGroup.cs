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
        // Build icons list robustly (include inactive)
        var allIcons = FindObjectsByType<CamIcon>(FindObjectsSortMode.None);
        int k = 0;
        for (int i = 0; i < allIcons.Length; i++)
        {
            if (allIcons[i] == null) continue;
            // compare underlying integer values to avoid cross-type enum == error
            if ((int)allIcons[i]._group == (int)_group)
            {
                if (icons == null) icons = new GameObject[4];
                if (k < icons.Length)
                    icons[k] = allIcons[i].gameObject;
                k++;
                if (k >= (icons?.Length ?? 4)) k = 0;
            }
        }
    }

    void Start()
    {
        // audio source (may be shared)
        var s = GameObject.Find("MonitorGroupBleeps");
        if (s != null) audioSource = s.GetComponent<AudioSource>();

        // Populate cameras[] robustly including inactive CamImage objects
        var allCamImages = FindObjectsByType<CamImage>(FindObjectsSortMode.None);
        int j = 0;
        if (allCamImages != null)
        {
            if (cameras == null) cameras = new GameObject[4];
            for (int i = 0; i < allCamImages.Length; i++)
            {
                if (allCamImages[i] == null) continue;
                // compare underlying integer values to avoid cross-type enum == error
                if ((int)allCamImages[i]._group == (int)_group)
                {
                    if (j < cameras.Length)
                        cameras[j] = allCamImages[i].gameObject;
                    j++;
                    if (j >= cameras.Length) j = 0;
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
        var img = GetComponent<Image>();
        if (img != null) img.color = currentColor;
    }

    // made public so manager can set selection state
    public void MakeSelected()
    {
        currentColor = selectedColor;
        selectedGroup = true;
    }
    public void Deselect()
    {
        currentColor = regularColor;
        selectedGroup = false;
    }

    // simplified: delegate centralized activation to CamGroupManager
    public void ReplaceCameras()
    {
        // play blip immediately if available
        if (audioSource != null && blip != null)
            audioSource.PlayOneShot(blip);

        // Centralize activation so only one group is ever active (works even if floor parents are disabled)
        CamGroupManager.Instance?.SetActiveGroup(this);
    }

    // Keep existing light/icon helpers public so manager can call them
    public void ReplaceLights()
    {
        //Improve performance by turning off the lights when the camera attached to those lights is not looking
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

    public void ReplaceIcons()
    {
        //Deselect all camIcons
        GameObject[] allIcons = GameObject.FindGameObjectsWithTag("CamIcon");
        for (int i = 0; i < allIcons.Length; i++)
        {
            allIcons[i].GetComponent<CamIcon>().Deselect();
        }
        //Then re-enable the icons that are part of the group
        if (icons != null)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].GetComponent<CamIcon>().Select();
            }
        }
    }

    IEnumerator StartSetUp()
    {
        yield return new WaitForSeconds(.2f);
        ReplaceCameras();
        FindAnyObjectByType<DisableCams>().SwitchEm();
    }
}