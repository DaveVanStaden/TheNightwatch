using System.Collections;
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

    public enum CursorType
    {
        shrink,
        stretch
    };
    public CursorType _type;


    Vector3 cursorScale;

    public float stretchDampening = 2f;
    public float stretchMaxLength = 5f;
    public float stretchMinWidth = 0.5f;
    public float updateRate = 120f;
    private float elapsed;

    public float shrinkMax;
    public float defaultSize = 1f;
    public float shrinkSpeed = .15f;

    [SerializeField] Color defaultColor;
    [SerializeField] Color clickColor;

    CamGroup selectedGroup;
    CamImage selectedCam;

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
            Vector2 prevcursorPos = new(cursorPos.x, cursorPos.y);

            cursorPos.x += Input.GetAxis("Mouse X") * cursorSpeed;
            cursorPos.y += Input.GetAxis("Mouse Y") * cursorSpeed;
            cursorPos.x = Mathf.Clamp(cursorPos.x, -posXlimit, posXlimit);
            cursorPos.y = Mathf.Clamp(cursorPos.y, -posYlimit, posYlimit);

            transform.localPosition = cursorPos;

            if (_type == CursorType.stretch)
            {
                elapsed += Time.deltaTime;
                if (elapsed > 1f / updateRate)
                {
                    elapsed = 0f;
                    float targetScaleX = Vector2.Distance(prevcursorPos, cursorPos) / stretchDampening;
                    targetScaleX = Mathf.Clamp(targetScaleX, 1f, stretchMaxLength);
                    float targetScaleY = Mathf.Clamp(1 / (targetScaleX / 1.5f), stretchMinWidth, 1f);
                    cursorScale = new(targetScaleX, targetScaleY, 1f);
                    transform.localScale = cursorScale;

                    float xDiff = prevcursorPos.x - cursorPos.x;
                    float yDiff = prevcursorPos.y - cursorPos.y;
                    float newRot = Mathf.Atan2(yDiff, xDiff) * 180 / Mathf.PI;
                    transform.rotation = Quaternion.Euler(0f, 0f, newRot);
                }
            }

            if (Input.GetKeyDown(KeyCode.Mouse0))
                clickL = true;
            else clickL = false;

            if (Input.GetKeyDown(KeyCode.Mouse1))
                clickR = true;
            else clickR = false;

            if (clickL || clickR)
            {
                GetComponent<RawImage>().color = clickColor;

                if (_type == CursorType.shrink)
                    StartCoroutine(ClickShrink());

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

            if (Input.GetKeyUp(KeyCode.Mouse0) || Input.GetKeyUp(KeyCode.Mouse1))
            {
                GetComponent<RawImage>().color = defaultColor;
                if (_type == CursorType.shrink)
                    StartCoroutine(ClickGrow());
            }

            if (selectedCam != null)
            {
                if (clickL || clickR)
                {
                    StartCoroutine(selectedCam.ChangeScale());
                }
            }
            if (selectedGroup != null)
            {
                if (clickL || clickR)
                {
                    selectedGroup.ReplaceCameras();
                }
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (beingControlled)
        {
            if (collision.CompareTag("CameraView"))
                selectedCam = collision.GetComponent<CamImage>();
            //if (collision.CompareTag("GroupButton"))
            //{
            //    if (clickL || clickR)
            //    {
            //        CamGroup camera = collision.GetComponent<CamGroup>();
            //        camera.ReplaceCameras();
            //    }
            //}
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        //Debug.Log("Entering " + collision.name);

        if (collision.CompareTag("GroupButton"))
        {
            CamGroup camera = collision.GetComponent<CamGroup>();
            camera.currentColor = camera.hoverColor;
            selectedGroup = camera;
        }

        //if (collision.CompareTag("VirtualWindow"))
        //{

        //}
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        //Debug.Log("Leaving " + collision.name);

        if (collision.CompareTag("GroupButton"))
        {
            selectedGroup = null;
            CamGroup camera = collision.GetComponent<CamGroup>();
            if (camera.selectedGroup)
            {
                camera.currentColor = camera.selectedColor;
            }
            else
                camera.currentColor = camera.regularColor;
        }
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
    IEnumerator ClickShrink()
    {
        Vector3 newscale;
        float timePassed = 0f;
        float pos;
        float maxTime = shrinkSpeed;

        while (timePassed < maxTime)
        {
            timePassed += Time.deltaTime;
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);

            newscale = Vector3.Lerp(new Vector3(defaultSize, defaultSize, 1f), new Vector3(shrinkMax, shrinkMax, 1f), pos);
            transform.localScale = newscale;
            yield return null;
        }
        yield return new WaitForSeconds(maxTime);
    }
    IEnumerator ClickGrow()
    {
        Vector3 newscale;
        float timePassed = 0f;
        float pos;
        float maxTime = shrinkSpeed;

        while (timePassed < maxTime)
        {
            timePassed += Time.deltaTime;
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);

            newscale = Vector3.Lerp(new Vector3(shrinkMax, shrinkMax, 1f), new Vector3(defaultSize, defaultSize, 1f), pos);
            transform.localScale = newscale;
            yield return null;
        }
        yield return new WaitForSeconds(maxTime);
    }
}
