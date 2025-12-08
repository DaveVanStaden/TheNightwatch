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

        if (tm == null)
        {
            Debug.LogWarning("[TaskChecker] No TaskManager found in Awake. Task list UI will update once a TaskManager exists.");
            return;
        }

        CheckTaskList();
    }

    public void CheckCompletedTasks()
    {
        if (tm == null) return;

        completedText.text = "Completed tasks: ";
        foreach (string task in tm.completedTasks)
        {
            completedText.text += "<br>- " + task;
        }
        CheckTaskList();
    }
    public void CheckTaskList()
    {
        if (tm == null)
        {
            if (text != null)
                text.text = "<u>Tasks: </u>\n(no TaskManager)";
            return;
        }

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
