using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class Interactable : MonoBehaviour
{
    [SerializeField] GameObject[] setAngles;
    Vector3 CamPrevPos;
    Quaternion CamPrevRot;
    float CamPrevFov;
    FPSController player;
    FlashlightRotator flashlightRotator;
    GameObject flashlight;
    public bool busy;

    AudioSource swoosh;
    private void Start()
    {
        player = FindObjectOfType<FPSController>();
        flashlightRotator = FindObjectOfType<FlashlightRotator>();
        flashlight = flashlightRotator.gameObject;
        swoosh = GetComponent<AudioSource>();
    }
    public void DoTheThing()
    {
        CamPrevPos = Camera.main.gameObject.transform.position;
        CamPrevRot = Camera.main.gameObject.transform.rotation;
        CamPrevFov = Camera.main.fieldOfView;
        Flashlight light = GameObject.Find("Flashlight").GetComponent<Flashlight>();
        Flashlight light2 = GameObject.Find("Extra light").GetComponent<Flashlight>();
        Camera.main.gameObject.transform.SetParent(null);
        flashlightRotator.canMove = false;


        //Cursor.lockState = CursorLockMode.None;
        //Cursor.visible = true;

        player.canMove = false;
        player.inView = true;

        //if (light.onOrOff)
        //{
        //    light.SwitchLight();
        //    light2.SwitchLight();
        //}

        PlaySound();
        StartCoroutine(SetAngle(0));
        flashlight.SetActive(false);
        //foreach (MonitorScreens monitor in FindObjectsOfType<MonitorScreens>())
        {

        }
    }
    public IEnumerator LeaveTheThing()
    {
        flashlight.SetActive(true);
        busy = true;

        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        float timePassed = 0f;
        float pos;
        float maxTime = .2f;
        while (timePassed < maxTime)
        {
            timePassed += Time.deltaTime;
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, CamPrevPos, pos);
            Camera.main.transform.rotation = Quaternion.Lerp(Camera.main.transform.rotation, CamPrevRot, pos);
            Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, CamPrevFov, pos);

            yield return null;
        }
        Camera.main.gameObject.transform.SetParent(player.gameObject.transform);
        player.inView = false;
        player.canMove = true;
        busy = false;
        flashlightRotator.canMove = true;
        yield return null;
    }

    public IEnumerator SetAngle(int angle)
    {
        CamData camera = setAngles[angle].GetComponent<CamData>();

        PlaySound();
        float timePassed = 0f;
        float pos;
        float maxTime = .15f;

        if (camera.hasCursor == true && camera != null)
        {
            camera.cursor.GetComponent<MonitorCursor>().EnableCursorControl();
        }

        while (timePassed < maxTime)
        {
            timePassed += Time.deltaTime;
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);
            Camera.main.transform.SetPositionAndRotation(Vector3.Lerp(Camera.main.transform.position, setAngles[angle].transform.position, pos), Quaternion.Lerp(Camera.main.transform.rotation, setAngles[angle].transform.rotation, pos));
            if (camera != null)
            {
                Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, camera.newFOV, pos);
            }
            yield return null;
        }
        yield return new WaitForSeconds(maxTime);
    }

    public void PlaySound()
    {
        swoosh.pitch = Random.Range(.20f, .30f);
        swoosh.PlayOneShot(swoosh.clip);
    }
}
