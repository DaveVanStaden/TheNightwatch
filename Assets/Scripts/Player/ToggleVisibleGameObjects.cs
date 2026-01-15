using UnityEngine;

public class ToggleVisibleGameObjects : MonoBehaviour
{
    bool visible;
    [SerializeField] bool hideAtStart;
    [SerializeField] GameObject[] objectsToShow;
    [SerializeField] GameObject[] objectsToHide;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (hideAtStart)
            SwapVisible();
    }

    public void SwapVisible()
    {
        if (!visible)
        {
            visible = true;
            Show();
        }
        else
        {
            visible = false;
            Hide();
        }
    }
    public void Show()
    {
        if (objectsToShow != null)
        {
            foreach (var obj in objectsToShow)
                obj.SetActive(true);
        }
    }
    public void Hide()
    {
        if (objectsToHide != null)
        {
            foreach (var obj in objectsToHide)
                obj.SetActive(false);
        }
    }
}
