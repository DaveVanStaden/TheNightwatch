using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public bool tutorialCompleted = false;
    public int tutorialIndex = 0;

    public GameObject[] tutorialTargets;
    void Start()
    {
        tutorialTargets = GameObject.FindGameObjectsWithTag("Tutorial");
        if (!tutorialCompleted)
        {
            BeginTutorial();
        }
        else DespawnTutorial();
    }

    private void BeginTutorial()
    {
        foreach (Interactable interactable in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
        {
            interactable.tutorialActive = true;
        }
        ;
    }
    private void DespawnTutorial()
    {
        foreach (GameObject target in tutorialTargets)
        {
            target.SetActive(false);
        }
    }

    public void UpdateTutorial()
    {
        tutorialIndex++;
        if (tutorialIndex > 4)
        {
            tutorialCompleted = true;
            DespawnTutorial();
        }
    }
}
