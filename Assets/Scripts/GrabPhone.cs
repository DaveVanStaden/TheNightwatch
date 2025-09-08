using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabPhone : MonoBehaviour
{
    [SerializeField] AudioClip ring;
    [SerializeField] AudioClip call;
    [SerializeField] AudioClip dial;
    [SerializeField] AudioClip shatter;
    [SerializeField] AudioClip thud;
    AudioSource speakers;
    AudioSource phone;

    Vector3 currentPosition;
    Quaternion currentRotation;
    public Transform targetPosition;

    [SerializeField] Material offMaterial;
    public bool isAttatched = false;

    // Start is called before the first frame update
    void Start()
    {
        speakers = GetComponent<AudioSource>();
        phone = GameObject.FindWithTag("Phone").GetComponent<AudioSource>();
    }

    public IEnumerator AttatchToFace()
    {
        transform.SetParent(targetPosition.gameObject.transform);
        float timePassed = 0f;
        float pos;
        float maxTime = 1f;

        while (timePassed < maxTime)
        {
            pos = Mathf.Lerp(0f, 1f, timePassed / maxTime);
            timePassed += Time.deltaTime;

            transform.GetPositionAndRotation(out Vector3 initialPosition, out Quaternion initialRotation);
            currentPosition = Vector3.Slerp(initialPosition, targetPosition.position, pos);
            currentRotation = Quaternion.Slerp(initialRotation, targetPosition.rotation, pos);
            transform.SetPositionAndRotation(currentPosition, currentRotation);
            //Debug.Log("Busyyyy");
            yield return null;
        }
        //Debug.Log("Done!");
        StartPhoneStuff();
        yield return null;
    }

    private void StartPhoneStuff()
    {
        speakers.Stop();
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.enabled = false;
        speakers.clip = call;
        speakers.loop = false;
        speakers.Play();
        isAttatched = true;
        StartCoroutine(WaitToFall());
    }

    IEnumerator WaitToFall()
    {
        yield return new WaitForSeconds(speakers.clip.length);
        StartCoroutine(Fall());

    }
    IEnumerator Fall()
    {
        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        rb.isKinematic = false;
        rb.angularVelocity += new Vector3(2f, 0f, 0f);
        transform.SetParent(null);

        MeshRenderer phoneMat = GameObject.FindWithTag("Phone").GetComponent<MeshRenderer>();
        Material[] phoneMats = new Material[phoneMat.sharedMaterials.Length];
        phoneMats[0] = phoneMat.sharedMaterials[0];
        phoneMats[1] = offMaterial;
        phoneMat.sharedMaterials = phoneMats;

        speakers.clip = dial;
        speakers.Play();
        yield return new WaitForSeconds(.8f);
        speakers.clip = shatter;
        phone.clip = thud;
        speakers.Play();
        phone.Play();
        yield return null;
    }
    public void SkipCall()
    {
        StopCoroutine(WaitToFall());
        StartCoroutine(Fall());
    }
}
