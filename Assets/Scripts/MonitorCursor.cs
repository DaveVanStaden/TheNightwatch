using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MonitorCursor : MonoBehaviour
{
    PlayerManager player;
    public bool beingControlled = false;
    public float cursorSpeed;
    Vector3 cursorPos;
    public float posXlimit;
    public float posYlimit;

    Vector3 cursorScale;
    public bool stretchyCursor;

    CamImage selectedCam;

    bool clickL;
    bool clickR;
    void Start()
    {
        player = FindObjectOfType<PlayerManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (beingControlled == true && player.currentLayer == 1)
        {
            Vector2 prevcursorPos = new(cursorPos.x, cursorPos.y);

            cursorPos.x += Input.GetAxis("Mouse X") * cursorSpeed;
            cursorPos.y += Input.GetAxis("Mouse Y") * cursorSpeed;
            cursorPos.x = Mathf.Clamp(cursorPos.x, -posXlimit, posXlimit);
            cursorPos.y = Mathf.Clamp(cursorPos.y, -posYlimit, posYlimit);

            transform.localPosition = cursorPos;

            if (stretchyCursor)
            {
                float targetScale = Vector2.Distance(prevcursorPos, cursorPos);
                targetScale = Mathf.Clamp(targetScale, 1f, 4f);
                cursorScale = new(targetScale, 1f, 1f);
                transform.localScale = cursorScale;

                float xDiff = prevcursorPos.x - cursorPos.x;
                float yDiff = prevcursorPos.y - cursorPos.y;
                float newRot = Mathf.Atan2(yDiff, xDiff) * 180 / Mathf.PI;
                transform.rotation = Quaternion.Euler(0f, 0f, newRot);
            }

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
