using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonitorCursor : MonoBehaviour
{
    FPSController player;
    public bool beingControlled = false;
    public float cursorSpeed;
    Vector3 cursorPos;
    public float posXlimit;
    public float posYlimit;

    CamImage selectedCam;

    bool clickL;
    bool clickR;
    void Start()
    {
        player = FindObjectOfType<FPSController>();
    }

    // Update is called once per frame
    void Update()
    {
        if (beingControlled == true && player.currentLayer == 1)
        {
            cursorPos.x += Input.GetAxis("Mouse X") * cursorSpeed;
            cursorPos.y += Input.GetAxis("Mouse Y") * cursorSpeed;
            cursorPos.x = Mathf.Clamp(cursorPos.x, -posXlimit, posXlimit);
            cursorPos.y = Mathf.Clamp(cursorPos.y, -posYlimit, posYlimit);

            transform.localPosition = cursorPos;

            if (Input.GetKeyDown(KeyCode.Mouse0)) clickL = true;
            else clickL = false;
            if (Input.GetKeyDown(KeyCode.Mouse1)) clickR = true;
            else clickR = false;

            if (selectedCam != null)
            {
                if (clickL)
                {
                    StartCoroutine(selectedCam.ChangeScale());
                }
                if (clickR)
                {
                    StartCoroutine(selectedCam.ChangeScale());
                }
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (beingControlled)
        selectedCam = collision.GetComponent<CamImage>();
    }

    public void EnableCursorControl()
    {
        beingControlled = true;
    }
    public void DisableCursorControl()
    {
        beingControlled = false;
    }
}
