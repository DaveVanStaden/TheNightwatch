using System.Collections;
using UnityEngine;

/// <summary>
/// Stores per-page flip targets and provides flip coroutines.
/// - Author sets `flippedLocalEuler` and `flippedLocalPosition` in Inspector for each page (local space).
/// - Page will rotate to `flippedLocalEuler` then translate to `flippedLocalPosition`.
/// - Use StartImmediateFlipped(true/false) to set initial state.
/// </summary>
[DisallowMultipleComponent]
public class TutorialPage : MonoBehaviour
{
    [Tooltip("Local Euler angles (degrees) the page should have when flipped.")]
    public Vector3 flippedLocalEuler = new Vector3(0f, -180f, 0f);

    [Tooltip("Local position the page should move to after flipping (local space).")]
    public Vector3 flippedLocalPosition = Vector3.zero;

    // cached original transforms (local)
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
    }

    /// <summary>
    /// Immediately set the page to flipped/unflipped without animation.
    /// </summary>
    public void SetImmediateFlipped(bool flipped)
    {
        if (flipped)
        {
            transform.localRotation = Quaternion.Euler(flippedLocalEuler);
            transform.localPosition = flippedLocalPosition;
        }
        else
        {
            transform.localRotation = originalLocalRotation;
            transform.localPosition = originalLocalPosition;
        }
    }

    /// <summary>
    /// Animate flipping forward: rotate to flippedLocalEuler then move to flippedLocalPosition.
    /// rotationPortion controls how much of totalTime is consumed by rotation (0..1).
    /// </summary>
    public IEnumerator FlipForward(float totalTime, float rotationPortion = 0.6f)
    {
        float rotTime = Mathf.Max(0.01f, totalTime * rotationPortion);
        float moveTime = Mathf.Max(0.01f, totalTime - rotTime);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = Quaternion.Euler(flippedLocalEuler);

        float elapsed = 0f;
        while (elapsed < rotTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rotTime));
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.localRotation = endRot;

        Vector3 startPos = transform.localPosition;
        Vector3 endPos = flippedLocalPosition;
        elapsed = 0f;
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveTime));
            transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        transform.localPosition = endPos;
    }

    /// <summary>
    /// Animate flipping backward: move back to original position then rotate back to original rotation.
    /// Backwards performs move first, then rotate (provides nicer visual reverse).
    /// </summary>
    public IEnumerator FlipBackward(float totalTime, float rotationPortion = 0.6f)
    {
        float rotTime = Mathf.Max(0.01f, totalTime * rotationPortion);
        float moveTime = Mathf.Max(0.01f, totalTime - rotTime);

        // move first back to original
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = originalLocalPosition;
        float elapsed = 0f;
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveTime));
            transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        transform.localPosition = endPos;

        // then rotate back
        Quaternion startRot = transform.localRotation;
        Quaternion endRot = originalLocalRotation;
        elapsed = 0f;
        while (elapsed < rotTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rotTime));
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.localRotation = endRot;
    }
}