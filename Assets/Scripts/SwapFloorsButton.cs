using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SwapFloorsButton : MonoBehaviour
{
    Button button;
    [SerializeField] Image map1;
    [SerializeField] Image map2;
    [SerializeField] GameObject map1Cams;
    [SerializeField] GameObject map2Cams;

    [SerializeField] private MonitorCursor cursor;

    private bool isSwapped;
    private bool hovering;
    private void Start()
    {
        button = GetComponent<Button>();
        map2.enabled = false;
        map2Cams.SetActive(false);
    }

    private void Update()
    {
        if (cursor.beingControlled)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0) && hovering)
            {
                button.onClick.Invoke();
            }
            if (Input.GetKey(KeyCode.Mouse0) && hovering)
            {
                button.interactable = false;
            }
            if (Input.GetKeyUp(KeyCode.Mouse0))
            {
                button.interactable = true;
                if (hovering)
                {
                    button.Select();
                }
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<MonitorCursor>() == cursor)
        {
            button.Select();
            hovering = true;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.GetComponent<MonitorCursor>() == cursor)
        {
            hovering = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void SwapFloors()
    {
        if (!isSwapped)
        {
            map1.enabled = false;
            map1Cams.SetActive(false);

            map2.enabled = true;
            map2Cams.SetActive(true);

            isSwapped = true;
        }
        else
        {
            map1.enabled = true;
            map1Cams.SetActive(true);

            map2.enabled = false;
            map2Cams.SetActive(false);

            isSwapped = false;
        }

        // ensure the manager reapplies the selected group for the now-visible map
        if (CamGroupManager.Instance != null)
            CamGroupManager.Instance.ReapplyActiveGroupForVisibility();
    }
}
