using UnityEngine;

public class CamData : MonoBehaviour
{
    public float newFOV;
    public bool hasCursor;
    public MonitorCursor cursor;
    public bool disableWhenNotLooking = true;
    public ManualCameraRenderer camToDisable;

    public void DisableRendering()
    {
        if (disableWhenNotLooking && camToDisable != null)
        {
            camToDisable.enabled = false;
        }
    }

    public void EnableRendering()
    {
        if (disableWhenNotLooking && camToDisable != null)
        {
            camToDisable.enabled = true;
        }
    }
}
