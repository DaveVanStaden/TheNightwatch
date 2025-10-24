using UnityEngine;
using UnityEngine.Rendering;

public class PostProcessingChanges : MonoBehaviour
{
    private PlayerStats stats;
    [SerializeField] private Volume pp;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stats = GetComponent<PlayerStats>();
    }

    // Update is called once per frame
    void Update()
    {
        pp.weight = 1f - (stats.Sanity/100f);

    }
}
