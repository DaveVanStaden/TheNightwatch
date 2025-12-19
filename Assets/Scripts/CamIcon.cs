using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CamIcon : MonoBehaviour
{
    private Image currentIcon;
    public Sprite selectedIcon;
    public Sprite deselectedIcon;
    TextMeshProUGUI text;

    [SerializeField] private GameObject viewArea;

    public enum Group
    {
        A, B, C, D, E, F
    };
    public Group _group;

    [SerializeField] private float selectedSize = 1f;
    [SerializeField] private float deselectedSize = .5f;

    void Awake()
    {
        currentIcon = GetComponent<Image>();
        text = GetComponentInChildren<TextMeshProUGUI>();
        Deselect();
    }

    public void Select()
    {
        transform.localScale = new Vector3(selectedSize, selectedSize, 1f);
        currentIcon.sprite = selectedIcon;
        text.color = Color.black;
        viewArea.SetActive(true);
    }
    public void Deselect()
    {
        transform.localScale = new Vector3(deselectedSize, deselectedSize, 1f);
        currentIcon.sprite = deselectedIcon;
        text.color = Color.grey;
        viewArea.SetActive(false);
    }
}
