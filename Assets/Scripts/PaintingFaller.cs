using System.Collections;
using UnityEngine;

public class PaintingFaller : MonoBehaviour
{
    BoxCollider box;
    TaskManager taskManager;
    [SerializeField] AudioSource thud;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        box = GetComponent<BoxCollider>();
        taskManager = FindAnyObjectByType<TaskManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(taskManager.specialCamImage.ForceZoomTrue());
            StartCoroutine(PlaySound());
            //Debug.Log("Player entering forced painting fall zone");
        }
    }
    private IEnumerator PlaySound()
    {
        yield return new WaitForSeconds(.45f);
        thud.Play();
    }
}
