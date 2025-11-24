using System.Collections;
using UnityEngine;

public class CamData : MonoBehaviour
{
    public float newFOV;
    public bool hasCursor;
    public MonitorCursor cursor;
    public bool disableWhenNotLooking = true;
    public ManualCameraRenderer camToDisable;
    public GameObject tutorialOverlay;

    public void DisableRendering()
    {
        if (disableWhenNotLooking && camToDisable != null)
        {
            camToDisable.fps = 1f;
        }
    }

    public void EnableRendering()
    {
        if (disableWhenNotLooking && camToDisable != null)
        {
            camToDisable.fps = 120f;
        }
    }
    public IEnumerator RemoveTutorial()
    {
        yield return new WaitForSeconds(.2f);
        tutorialOverlay.SetActive(false);
        FindFirstObjectByType<TutorialManager>().UpdateTutorial();
    }
}
