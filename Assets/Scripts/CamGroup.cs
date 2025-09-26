using System.Runtime.CompilerServices;
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
         for (int i = 0; i<cameraViews.Length; i++)
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
    }
}
