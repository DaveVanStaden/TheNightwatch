using UnityEngine;

public class PhysicalCursor : MonoBehaviour
{
    PlayerManager player;
    public bool beingControlled = false;
    public float cursorSpeed;
    Vector3 cursorPos;
    public float posXlimit;
    public float posYlimit;

    GameObject selectedButton;

    [SerializeField] bool randomisedPitch;
    [SerializeField] float pitchOverride = 1f;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip sound;

    bool clickL;
    bool clickR;
    void Start()
    {
        player = FindObjectOfType<PlayerManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (beingControlled == true)
        {
            cursorPos.x += Input.GetAxis("Mouse X") * cursorSpeed;
            cursorPos.y += Input.GetAxis("Mouse Y") * cursorSpeed;
            cursorPos.x = Mathf.Clamp(cursorPos.x, -posXlimit, posXlimit);
            cursorPos.y = Mathf.Clamp(cursorPos.y, -posYlimit, posYlimit);

            transform.localPosition = cursorPos;

            if (Input.GetKeyDown(KeyCode.Mouse0))
                clickL = true;
            else clickL = false;

            if (Input.GetKeyDown(KeyCode.Mouse1))
                clickR = true;
            else clickR = false;

            if (clickL || clickR)
            {
                if (audioSource != null)
                {
                    if (sound != null)
                    {
                        if (randomisedPitch)
                        {
                            audioSource.pitch = Random.Range(0.95f, 1.05f);
                            audioSource.PlayOneShot(sound);
                        }
                        else
                        {
                            audioSource.pitch = pitchOverride;
                            audioSource.PlayOneShot(sound);
                        }
                    }
                }
            }

            if (selectedButton != null)
            {
                if (clickL || clickR)
                {
                    //StartCoroutine(selectedButton.ChangeScale());
                }
            }
        }
    }

    private void OnTriggerStay(Collider collision)
    {
        if (beingControlled)
        {
            //if (collision.CompareTag("CameraView"))
            //    selectedButton = collision.GetComponent<CamImage>();
        }
    }
    private void OnTriggerEnter(Collider collision)
    {
        //Debug.Log("Entering " + collision.name);

        //if (collision.CompareTag("GroupButton"))
        //{
        //    CamGroup camera = collision.GetComponent<CamGroup>();
        //    camera.currentColor = camera.hoverColor;
        //    selectedGroup = camera;
        //}

        //if (collision.CompareTag("VirtualWindow"))
        //{

        //}
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        //Debug.Log("Leaving " + collision.name);

        //if (collision.CompareTag("GroupButton"))
        //{
            ////selectedGroup = null;
            //GameObject camera = collision.GetComponent<CamGroup>();
            //if (camera.selectedGroup)
            //{
            //    camera.currentColor = camera.selectedColor;
            //}
            //else
            //    camera.currentColor = camera.regularColor;
        //}
    }

    public void EnableCursorControl()
    {
        beingControlled = true;
    }
    public void DisableCursorControl()
    {
        Debug.Log("Disabling: " + name);
        beingControlled = false;
    }
}
