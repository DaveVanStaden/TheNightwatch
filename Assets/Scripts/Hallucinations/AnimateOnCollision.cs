using UnityEngine;

public class AnimateOnCollision : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] bool playSound;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] clips;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            animator.SetTrigger("play");
            if (playSound)
                audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)]);
            GetComponent<BoxCollider>().enabled = false;
        }
    }
}
