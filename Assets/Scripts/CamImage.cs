using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CamImage : MonoBehaviour
{
    bool isBig = false;
    public enum Group
    {
        A, B, C, D, E, F
    };
    public Group _group;

    Vector2 currentSize;
    Vector2 currentAnchorMin;
    Vector2 currentAnchorMax;
    Vector2 currentPivot;
    Vector2 currentCollider;
    Vector2 colliderOffset;
    RectTransform rect;
    BoxCollider2D imgCollider;

    public SecurityCamera originalCam;

    [SerializeField] AudioSource audioSource;
    //[SerializeField] AudioClip expand;
    [SerializeField] AudioClip collapse;

    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    void Start()
    {
        rect = GetComponent<RectTransform>();
        imgCollider = GetComponent<BoxCollider2D>();
        currentSize = rect.sizeDelta;
        currentAnchorMin = rect.anchorMin;
        currentAnchorMax = rect.anchorMax;
        currentPivot = rect.pivot;
        currentCollider = imgCollider.size;
        colliderOffset = imgCollider.offset;
        audioSource = GameObject.Find("MonitorViewBleeps").GetComponent<AudioSource>();
    }

    public IEnumerator ChangeScale()
    {
        if (!isBig)
        {
            isBig = true;

            foreach (CamImage camera in FindObjectsByType<CamImage>(FindObjectsSortMode.None))
            {
                if (camera._group == _group)
                {
                    camera.gameObject.GetComponent<RawImage>().enabled = false;
                    camera.gameObject.GetComponent<Collider2D>().enabled = false;
                    camera.titleText.enabled = false;
                    camera.descText.enabled = false;

                    gameObject.GetComponent<RawImage>().enabled = true;
                    gameObject.GetComponent<Collider2D>().enabled = true;
                    titleText.enabled = true;
                    descText.enabled = true;
                }

            }
            float timePassed = 0f;
            float pos;
            float maxTime = .1f;
            while (timePassed < maxTime)
            {
                timePassed += Time.deltaTime;
                pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);
                Vector2 mid = new(0.5f, 0.5f);
                rect.anchorMin = mid;
                rect.anchorMax = mid;
                rect.pivot = mid;

                rect.anchorMin = Vector2.Lerp(currentAnchorMin, mid, pos);
                rect.anchorMax = Vector2.Lerp(currentAnchorMax, mid, pos);
                rect.pivot = Vector2.Lerp(currentPivot, mid, pos);
                rect.sizeDelta = Vector2.Lerp(currentSize, new Vector2(256, 256), pos);
                yield return null;
            }
            imgCollider.size = new Vector2(256, 256);
            imgCollider.offset = Vector2.zero;
            //audioSource.pitch = 1f;
            //audioSource.PlayOneShot(expand);
            yield return null;
        }
        else
        {
            rect.sizeDelta = currentSize;
            rect.anchorMin = currentAnchorMin;
            rect.anchorMax = currentAnchorMax;
            rect.sizeDelta = currentSize;
            rect.pivot = currentPivot;
            imgCollider.size = currentCollider;
            imgCollider.offset = colliderOffset;

            isBig = false;
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(collapse);
            foreach (CamImage camera in FindObjectsByType<CamImage>(FindObjectsSortMode.None))
            {
                if (camera._group == _group)
                {
                    camera.gameObject.GetComponent<RawImage>().enabled = true;
                    camera.gameObject.GetComponent<Collider2D>().enabled = true;

                    camera.titleText.enabled = true;
                    camera.descText.enabled = true;
                }
            }
        }
    }

    // Add this helper to immediately collapse an enlarged camera without animation.
    // Called by CamGroupManager when switching groups so a zoomed camera doesn't block new group rendering.
    public void CollapseInstant()
    {
        // ensure rect and collider are cached
        if (rect == null) rect = GetComponent<RectTransform>();
        if (imgCollider == null) imgCollider = GetComponent<BoxCollider2D>();

        if (!isBig) return;

        // stop any running animation/coroutine on this CamImage
        StopAllCoroutines();

        // restore transform/rect/collider to saved defaults
        rect.sizeDelta = currentSize;
        rect.anchorMin = currentAnchorMin;
        rect.anchorMax = currentAnchorMax;
        rect.pivot = currentPivot;

        if (imgCollider != null)
        {
            imgCollider.size = currentCollider;
            imgCollider.offset = colliderOffset;
        }

        isBig = false;
    }

    // Expose the zoom state with a public accessor (no other changes)
    public bool IsZoomed()
    {
        return isBig;
    }
    public IEnumerator ForceZoomTrue()
    {
        if (isBig)
            IsZoomed();
        else
        {
            isBig = true;
            IsZoomed();
            yield return new WaitForSeconds(1);
            isBig = false;
        }
    }
}
