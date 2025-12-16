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

        // Subscribe a single updater so we can unsubscribe cleanly
        tm.OnTasksChanged += UpdateAll;

        UpdateAll();
    }

    private void OnDestroy()
    {
        if (tm != null)
            tm.OnTasksChanged -= UpdateAll;
    }

    // Central UI refresh called when TaskManager signals changes
    private void UpdateAll()
    {
        CheckTaskList();
        CheckCompletedTasks();
    }

    public void CheckCompletedTasks()
    {
        if (tm == null) return;

        if (completedText != null)
        {
            completedText.text = "Completed tasks: ";
            foreach (string task in tm.completedTasks)
            {
                var display = GetDisplayForTaskName(task, completed: true);
                completedText.text += "<br>- " + display;
            }
        }

        // also refresh ongoing list to keep UI consistent
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

        // Use the TaskManager's active task list (ongoing tasks)
        var active = tm.ActiveTasks;
        if (active == null || active.Count == 0)
        {
            text.text += "\n- (no active tasks)";
            return;
        }

        foreach (var task in active)
        {
            if (task == null) continue;

            string display;

            // Prefer concrete-type detection for active tasks so wording cannot be confused with completed wording.
            if (task is PaintingTask)
            {
                display = "Fix Paintings";
            }
            else if (task is TrashTask)
            {
                display = "Clean Trash";
            }
            else if (task is SinglePaintingFallTask)
            {
                display = "Fix Singular Painting";
            }
            else
            {
                // Fallback to name-based mapping (keeps existing behavior for unknown tasks)
                display = GetDisplayForTaskName(task.TaskName, completed: false);
            }

            text.text += "<br>- " + display;
        }
    }

    // Map internal task name -> friendly active/completed strings (string-based fallback)
    private string GetDisplayForTaskName(string taskName, bool completed)
    {
        if (string.IsNullOrEmpty(taskName)) return completed ? "Completed" : "Task";

        // Common concrete names used in tasks
        switch (taskName)
        {
            case "PaintingTask":
                return completed ? "Fixed Painting" : "Fix Paintings";
            case "TrashTask":
                return completed ? "Cleaned Trash" : "Clean Trash";
            case "SinglePaintingFall":
            case "SinglePaintingFallTask":
                return completed ? "Fixed Singular Painting" : "Fix Singular Painting";
            default:
                // Fallback: if completed show past-tense hint, otherwise show the task name
                return completed ? $"Completed: {taskName}" : taskName;
        }
    }
}
