using UnityEngine;

public class SpawnOnCollision : MonoBehaviour
{
    [SerializeField] GameObject objectToSpawn;
    [SerializeField] Transform objectSpawnTransform;
    [SerializeField] float sanityThreshold = 100f;

    [SerializeField]
    enum SpawnType
    {
        InstanciateObject, EnableExisting
    }
    [SerializeField] SpawnType _type;
    private void Start()
    {
        if (objectToSpawn != null)
        {
            if (objectToSpawn.activeInHierarchy)
            {
                objectToSpawn.SetActive(false);
            }
        }
    }

    // Update is called once per frame
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (objectToSpawn != null)
            {
                if (other.GetComponent<PlayerStats>().Sanity <= sanityThreshold)
                {
                    if (_type == SpawnType.InstanciateObject)
                        Instantiate(objectToSpawn, objectSpawnTransform.position, objectSpawnTransform.rotation);
                    if (_type == SpawnType.EnableExisting)
                        objectToSpawn.SetActive(true);
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.LogError("[SpawnOnCollision] Could not spawn object: No object specified");
            }
        }
    }
}
