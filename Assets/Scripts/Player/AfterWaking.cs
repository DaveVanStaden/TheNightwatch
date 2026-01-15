using System.Collections;
using UnityEngine;

public class AfterWaking : MonoBehaviour
{
    [SerializeField] AudioSource jazz;
    [SerializeField] float timeToWait;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (jazz != null)
        {
            StartCoroutine(PlayJazzAfterWake());
        }
    }

    IEnumerator PlayJazzAfterWake()
    {
        yield return new WaitForSeconds(timeToWait + 2f);
        jazz.Play();
    }

}
