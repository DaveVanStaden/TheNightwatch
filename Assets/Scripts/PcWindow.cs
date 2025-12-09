using UnityEngine;

public class PcWindow : MonoBehaviour
{
    MonitorCursor cursor;
    private bool isGrabbed;
    PcWindow overlappedWindow;
    BoxCollider2D bCollider;
    private void Start()
    {
        bCollider = GetComponent<BoxCollider2D>();
    }
    private void Update()
    {
        if (cursor != null)
        {
            if (cursor.beingControlled)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    Collider2D[] cols = Physics2D.OverlapCircleAll(cursor.transform.position, 0.001f);
                    if (cols[^1] == bCollider)
                    {
                        isGrabbed = true;
                    }
                    else isGrabbed = false;
                }
                if (Input.GetKey(KeyCode.Mouse0) && isGrabbed)
                {
                    transform.SetAsLastSibling();
                    transform.SetSiblingIndex(transform.GetSiblingIndex() - 2);
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
