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

    private TaskManager taskManager;
    private Coroutine waitToFallCoroutine;
    private Coroutine fallCoroutine;
    private MeshRenderer phoneMeshRenderer; // Cache the mesh renderer

    // Start is called before the first frame update
    void Start()
    {
        speakers = GetComponent<AudioSource>();
        phone = GameObject.FindWithTag("Phone").GetComponent<AudioSource>();
        taskManager = FindAnyObjectByType<TaskManager>();
        
        // Cache the phone mesh renderer on start - try to find it in the hierarchy
        GameObject phoneObj = GameObject.FindWithTag("Phone");
        if (phoneObj != null)
        {
            // First try to get MeshRenderer directly on the Phone object
            phoneMeshRenderer = phoneObj.GetComponent<MeshRenderer>();
            
            // If not found, search in children
            if (phoneMeshRenderer == null)
            {
                Debug.Log("[GrabPhone] MeshRenderer not on main Phone object, searching children...");
                phoneMeshRenderer = phoneObj.GetComponentInChildren<MeshRenderer>();
            }
            
            if (phoneMeshRenderer != null)
            {
                Debug.Log($"[GrabPhone] Found phone MeshRenderer on '{phoneMeshRenderer.gameObject.name}' with {phoneMeshRenderer.sharedMaterials.Length} materials");
                
                // Log all material names for debugging
                for (int i = 0; i < phoneMeshRenderer.sharedMaterials.Length; i++)
                {
                    Debug.Log($"[GrabPhone] Start Material {i}: {phoneMeshRenderer.sharedMaterials[i].name}");
                }
            }
            else
            {
                Debug.LogWarning("[GrabPhone] Phone GameObject found but has no MeshRenderer (even in children)!");
            }
        }
        else
        {
            Debug.LogWarning("[GrabPhone] Could not find GameObject with tag 'Phone'!");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X) && isAttatched)
        {
            SkipCall();
        }
    }
    public IEnumerator AttatchToFace()
    {
        if (!isAttatched)
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
    }

    public void ToFace()
    {
        StartCoroutine(AttatchToFace());
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
        waitToFallCoroutine = StartCoroutine(WaitToFall());
    }

    IEnumerator WaitToFall()
    {
        yield return new WaitForSeconds(speakers.clip.length);
        fallCoroutine = StartCoroutine(Fall(true));
    }
    IEnumerator Fall(bool skipDialTone = false)
    {
        // Detach phone and enable physics
        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.angularVelocity += new Vector3(2f, 0f, 0f);
        }
        
        transform.SetParent(null);

        // Play dial tone if not skipping (manual hang up)
        if (!skipDialTone && dial != null && speakers != null)
        {
            // Turn off phone screen immediately when hanging up
            TurnOffPhoneScreen();
            
            speakers.Stop();
            yield return null; // Wait one frame before changing clip
            speakers.clip = dial;
            speakers.loop = false;
            speakers.Play();
            
            Debug.Log("[GrabPhone] Playing dial tone");
            
            // Wait for dial tone to finish
            yield return new WaitForSecondsRealtime(0.8f);
        }
        else
        {
            // Turn off phone screen when call finishes naturally
            TurnOffPhoneScreen();
        }
        
        // Play shatter and thud sounds simultaneously using PlayOneShot
        if (speakers != null && shatter != null)
        {
            speakers.PlayOneShot(shatter);
            Debug.Log("[GrabPhone] Playing shatter sound");
        }
        
        if (phone != null && thud != null)
        {
            phone.PlayOneShot(thud);
            Debug.Log("[GrabPhone] Playing thud sound");
        }
        
        fallCoroutine = null;
        yield return null;
    }

    private void TurnOffPhoneScreen()
    {
        if (phoneMeshRenderer != null && offMaterial != null)
        {
            // Use materials instead of sharedMaterials to ensure instance modification works in builds
            Material[] phoneMats = phoneMeshRenderer.materials;
            Debug.Log($"[GrabPhone] Turning off screen - material count: {phoneMats.Length}");
            
            bool materialReplaced = false;
            for (int i = 0; i < phoneMats.Length; i++)
            {
                Debug.Log($"[GrabPhone] Material {i}: {phoneMats[i].name}");
                
                // Try to identify screen material by name or properties
                string matName = phoneMats[i].name.ToLower();
                
                // Replace if it's index 1, OR if the material name suggests it's a screen/emission material
                if (i == 1 || matName.Contains("screen") || matName.Contains("emission") || matName.Contains("light"))
                {
                    Debug.Log($"[GrabPhone] Found potential screen material at index {i}: {phoneMats[i].name}");
                    phoneMats[i] = offMaterial;
                    Debug.Log($"[GrabPhone] Replaced material {i} with offMaterial: {offMaterial.name}");
                    materialReplaced = true;
                }
            }
            
            if (!materialReplaced)
            {
                Debug.LogWarning("[GrabPhone] No screen material identified! Replacing index 1 as fallback.");
                if (phoneMats.Length > 1)
                {
                    phoneMats[1] = offMaterial;
                }
            }
            
            phoneMeshRenderer.materials = phoneMats;
            Debug.Log("[GrabPhone] Phone screen turned off - materials applied");
        }
        else
        {
            if (phoneMeshRenderer == null)
                Debug.LogWarning("[GrabPhone] Cannot turn off screen - phoneMeshRenderer is null!");
            if (offMaterial == null)
                Debug.LogWarning("[GrabPhone] Cannot turn off screen - offMaterial is null!");
        }
    }

    public void SkipCall()
    {
        // Set the flag in TaskManager to allow the tutorial painting to fall
        if (taskManager != null)
        {
            taskManager.specialPhoneCallStarted = true;
            Debug.Log("[GrabPhone] SkipCall pressed - setting specialPhoneCallStarted to true");
        }
        else
        {
            Debug.LogWarning("[GrabPhone] SkipCall pressed but TaskManager not found!");
        }

        // Stop the waiting coroutine using the stored reference
        if (waitToFallCoroutine != null)
        {
            StopCoroutine(waitToFallCoroutine);
            waitToFallCoroutine = null;
        }
        
        // Stop any existing Fall coroutine to prevent duplicates
        if (fallCoroutine != null)
        {
            StopCoroutine(fallCoroutine);
            fallCoroutine = null;
        }

        // Stop the call audio immediately
        if (speakers != null && speakers.isPlaying)
        {
            speakers.Stop();
        }
        
        // Play dial tone (hang up sound) before dropping
        fallCoroutine = StartCoroutine(Fall(false));
    }
}
