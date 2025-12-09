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

    // ensure arrays are present; code will handle null elements safely
    public GameObject[] cameras;
    public GameObject[] icons;

    private void Awake()
    {
        // Ensure icons array exists
        if (icons == null || icons.Length < 4) icons = new GameObject[4];

        GameObject[] iconList = GameObject.FindGameObjectsWithTag("CamIcon");
        int k = 0;
        for (int i = 0; i < iconList.Length; i++)
        {
            var iconComp = iconList[i].GetComponent<CamIcon>();
            if (iconComp == null) continue;
            if (iconComp._group.ToString() == _group.ToString())
            {
                // protect against index overflow
                if (k >= icons.Length)
                {
                    Debug.LogWarning($"[CamGroup:{name}] Not enough space in icons array, resizing.");
                    System.Array.Resize(ref icons, k + 1);
                }
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
        // safe audioSource lookup
        var mgrObj = GameObject.Find("MonitorGroupBleeps");
        if (mgrObj != null)
        {
            audioSource = mgrObj.GetComponent<AudioSource>();
            if (audioSource == null)
                Debug.LogWarning($"[CamGroup:{name}] 'MonitorGroupBleeps' found but no AudioSource attached.");
        }
        else
        {
            Debug.LogWarning($"[CamGroup:{name}] Could not find GameObject 'MonitorGroupBleeps' in scene.");
        }

        // Ensure cameras array exists
        if (cameras == null || cameras.Length < 4) cameras = new GameObject[4];

        GameObject[] cameralist = GameObject.FindGameObjectsWithTag("CameraView");
        int j = 0;
        for (int i = 0; i < cameralist.Length; i++)
        {
            var camImage = cameralist[i].GetComponent<CamImage>();
            if (camImage == null) continue;
            if (camImage._group.ToString() == _group.ToString())
            {
                if (j >= cameras.Length)
                {
                    Debug.LogWarning($"[CamGroup:{name}] Not enough space in cameras array, resizing.");
                    System.Array.Resize(ref cameras, j + 1);
                }
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
        var img = GetComponent<Image>();
        if (img != null)
            img.color = currentColor;
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
        if (audioSource != null && blip != null)
            audioSource.PlayOneShot(blip);

        //Disable everything, just for a moment
        //First disable the camera's views
        GameObject[] cameraViews = GameObject.FindGameObjectsWithTag("CameraView");
        for (int i = 0; i < cameraViews.Length; i++)
        {
            if (cameraViews[i] == null) continue;
            var raw = cameraViews[i].GetComponent<RawImage>();
            if (raw != null) raw.enabled = false;
            var bc = cameraViews[i].GetComponent<BoxCollider2D>();
            if (bc != null) bc.enabled = false;
            var camImg = cameraViews[i].GetComponent<CamImage>();
            if (camImg != null)
            {
                if (camImg.titleText != null) camImg.titleText.enabled = false;
                if (camImg.descText != null) camImg.descText.enabled = false;
            }
        }
        //Then deselect the other buttons
        GameObject[] groupButtons = GameObject.FindGameObjectsWithTag("GroupButton");
        for (int i = 0; i < groupButtons.Length; i++)
        {
            var cg = groupButtons[i].GetComponent<CamGroup>();
            if (cg != null) cg.Deselect();
        }


        //Now that everything is deselected, re-enable all the cameras that are part of our group
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null) continue;
            var raw = cameras[i].GetComponent<RawImage>();
            if (raw != null) raw.enabled = true;
            var bc = cameras[i].GetComponent<Collider2D>();
            if (bc != null) bc.enabled = true;
            var camImg = cameras[i].GetComponent<CamImage>();
            if (camImg != null)
            {
                if (camImg.titleText != null) camImg.titleText.enabled = true;
                if (camImg.descText != null) camImg.descText.enabled = true;
            }
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
            if (allLights[i] == null) continue;
            var sec = allLights[i].GetComponent<SecurityCamera>();
            if (sec != null && sec.lights)
                sec.DisableLight();
        }

        for (int i = 0; i < cameras.Length; i++)
        {
            var camObj = cameras[i];
            if (camObj == null) continue;
            var camImg = camObj.GetComponent<CamImage>();
            if (camImg == null) continue;
            var orig = camImg.originalCam;
            if (orig == null) continue;
            if (!orig.lights)
                orig.EnableLight();
        }
    }
    private void ReplaceIcons()
    {
        //Deselect all camIcons
        GameObject[] allIcons = GameObject.FindGameObjectsWithTag("CamIcon");
        for (int i = 0; i < allIcons.Length; i++)
        {
            var icon = allIcons[i].GetComponent<CamIcon>();
            if (icon != null) icon.Deselect();
        }
        //Then re-enable the icons that are part of the group
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null) continue;
            var ic = icons[i].GetComponent<CamIcon>();
            if (ic != null) ic.Select();
        }
    }

    IEnumerator StartSetUp()
    {
        yield return new WaitForSeconds(.2f);
        ReplaceCameras();
        FindAnyObjectByType<DisableCams>().SwitchEm();
    }
}
