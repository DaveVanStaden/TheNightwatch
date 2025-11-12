using UnityEngine;

public class PcWindow : MonoBehaviour
{
    MonitorCursor cursor;
    private bool isGrabbed;
    PcWindow overlappedWindow;
    private void Update()
    {
        if (cursor != null)
        {
            if (cursor.beingControlled)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    if (overlappedWindow != null)
                    {
                        if (transform.GetSiblingIndex() > overlappedWindow.transform.GetSiblingIndex())
                        {
                            isGrabbed = true;
                        }
                    }
                    else isGrabbed = true;
                }
                if (Input.GetKey(KeyCode.Mouse0) && isGrabbed)
                {
                    transform.SetAsLastSibling();
                    transform.SetSiblingIndex(transform.GetSiblingIndex() - 1);
                    transform.localPosition += new Vector3(Input.GetAxis("Mouse X") * cursor.cursorSpeed, Input.GetAxis("Mouse Y") * cursor.cursorSpeed, 0f);

                }
                if (Input.GetKeyUp(KeyCode.Mouse0))
                    isGrabbed = false;
            }
        }
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.GetComponent<MonitorCursor>() != null)
        {
            cursor = collision.GetComponent<MonitorCursor>();
        }
        if (collision.GetComponent<PcWindow>())
        {
            overlappedWindow = collision.GetComponent<PcWindow>();
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GetComponent<MonitorCursor>())
            cursor = null;
        if (collision.GetComponent<PcWindow>())
        {
            overlappedWindow = null;
        }
    }
}
