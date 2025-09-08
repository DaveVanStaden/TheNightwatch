using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CamImage : MonoBehaviour
{
    bool isBig = false;
    Vector2 currentSize;
    Vector2 currentAnchorMin;
    Vector2 currentAnchorMax;
    Vector2 currentPivot;
    Vector2 currentCollider;
    Vector2 colliderOffset;
    RectTransform rect;
    BoxCollider2D imgCollider;
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
    }

    public IEnumerator ChangeScale()
    {
        if (!isBig)
        {
            isBig = true;

            foreach (CamImage camera in FindObjectsOfType<CamImage>())
            {
                camera.gameObject.GetComponent<RawImage>().enabled = false;
                camera.gameObject.GetComponent<Collider2D>().enabled = false;
                gameObject.GetComponent<RawImage>().enabled = true;
                gameObject.GetComponent<Collider2D>().enabled = true;

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

                rect.anchorMin = Vector2.Lerp(currentAnchorMin, mid , pos);
                rect.anchorMax = Vector2.Lerp(currentAnchorMax, mid, pos);
                rect.pivot = Vector2.Lerp(currentPivot, mid, pos);
                rect.sizeDelta = Vector2.Lerp(currentSize, new Vector2(256, 256), pos);
                yield return null;
            }
            imgCollider.size = new Vector2(256, 256);
            imgCollider.offset = Vector2.zero;
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
            foreach (CamImage camera in FindObjectsOfType<CamImage>())
            {
                camera.gameObject.GetComponent<RawImage>().enabled = true;
                camera.gameObject.GetComponent<Collider2D>().enabled = true;
            }
        }
    }
}
