using System.Collections;
using TMPro;
using UnityEngine;

public class TaskChecker : MonoBehaviour
{
    private TaskManager tm;
    private TextMeshProUGUI text;
    [SerializeField] private TextMeshProUGUI completedText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        tm = FindAnyObjectByType<TaskManager>();
        text = GetComponent<TextMeshProUGUI>();
        text.text = "<u>Tasks: </u>";
        CheckTaskList();
    }

    public void CheckCompletedTasks()
    {
        completedText.text = "Completed tasks: ";
        foreach (string task in tm.completedTasks)
        {
            completedText.text += "<br>- " + task;
        }
        CheckTaskList();
    }
    public void CheckTaskList()
    {
        text.text = "<u>Tasks: </u>";
        foreach (var task in tm.tasks)
        {
            if (task.TaskName == "PaintingTask" && !task.IsCompleted)
            {
                text.text += "<br>- Fix the <b><u>fallen painting</b></u>";
            }
            if (task.TaskName == "TrashTask" && !task.IsCompleted)
            {
                text.text += "<br>- Clean up <b><u>trash</b></u> on the floor";
            }
        }
    }
}
