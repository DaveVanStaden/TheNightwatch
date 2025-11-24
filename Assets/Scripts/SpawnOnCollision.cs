using UnityEngine;

public class SpawnOnCollision : MonoBehaviour
{
    [SerializeField] GameObject objectToSpawn;
    [SerializeField] Transform objectSpawnTransform;

    [SerializeField] enum SpawnType
    {
        InstanciateObject, EnableExisting
    }
    [SerializeField] SpawnType _type;

    // Update is called once per frame
    private void OnTriggerEnter(Collider other)
    {
        if (objectToSpawn != null) 
        {
            if (other.CompareTag("Player"))
            {
                if (_type == SpawnType.InstanciateObject)
                Instantiate(objectToSpawn, objectSpawnTransform.position, objectSpawnTransform.rotation);
                if (_type == SpawnType.EnableExisting)
                    objectToSpawn.SetActive(true);
                Destroy(gameObject);
            }
        }
    }
}
