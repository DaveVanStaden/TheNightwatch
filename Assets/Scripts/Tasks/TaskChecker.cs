using System.Collections;
using TMPro;
using UnityEngine;

public class TaskChecker : MonoBehaviour
{
    private TaskManager tm;
    private TextMeshProUGUI text;
    [SerializeField] private TextMeshProUGUI completedText;

    [Header("Task display strings (editable in inspector)")]
    [Tooltip("Active-format for Painting task. Use {0} for cleaned, {1} for total. If you remove placeholders the counts will be appended automatically.")]
    [SerializeField] private string paintingActiveFormat = "Fix Paintings ({0}/{1})";
    [Tooltip("Short active text for Painting when you prefer not to include counts in the format.")]
    [SerializeField] private string paintingActiveShort = "Fix Paintings";
    [Tooltip("Completed text for Painting task.")]
    [SerializeField] private string paintingCompletedText = "Fixed Paintings";

    [Tooltip("Active-format for Trash task. Use {0} for cleaned, {1} for total. If you remove placeholders the counts will be appended automatically.")]
    [SerializeField] private string trashActiveFormat = "Clean Trash ({0}/{1})";
    [Tooltip("Short active text for Trash when you prefer not to include counts in the format.")]
    [SerializeField] private string trashActiveShort = "Clean Trash";
    [Tooltip("Completed text for Trash task.")]
    [SerializeField] private string trashCompletedText = "Cleaned Trash";

    [Tooltip("Active text for SinglePaintingFall task.")]
    [SerializeField] private string singlePaintingActiveText = "Fix Singular Painting";
    [Tooltip("Completed text for SinglePaintingFall task.")]
    [SerializeField] private string singlePaintingCompletedText = "Fixed Singular Painting";

    [Tooltip("Active text for Finale task.")]
    [SerializeField] private string finaleActiveText = "Finale Task";
    [Tooltip("Completed text for Finale task.")]
    [SerializeField] private string finaleCompletedText = "Finale Complete";

    [Tooltip("Heading shown above the active task list.")]
    [SerializeField] private string tasksHeading = "<u>Tasks: </u>";
    [Tooltip("Heading shown above the completed tasks list.")]
    [SerializeField] private string completedHeading = "Completed tasks: ";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        tm = FindAnyObjectByType<TaskManager>();
        text = GetComponent<TextMeshProUGUI>();
        text.text = tasksHeading;

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
            completedText.text = completedHeading;
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
                text.text = tasksHeading + "\n(no TaskManager)";
            return;
        }

        text.text = tasksHeading;

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
            if (task is PaintingTask ptask)
            {
                // show cleaned/total chosen paintings (cleaned increases as player returns paintings)
                int total = ptask.TotalChosen;
                int remaining = ptask.RemainingToReturn;
                int cleaned = Mathf.Clamp(total - remaining, 0, total);
                display = FormatWithCounts(paintingActiveFormat, paintingActiveShort, cleaned, total);
            }
            else if (task is TrashTask ttask)
            {
                // show cleaned/total spawned trash (cleaned increases as player picks up trash)
                int total = ttask.TotalSpawned;
                int remaining = ttask.RemainingTrash;
                int cleaned = Mathf.Clamp(total - remaining, 0, total);
                display = FormatWithCounts(trashActiveFormat, trashActiveShort, cleaned, total);
            }
            else if (task is SinglePaintingFallTask)
            {
                display = singlePaintingActiveText;
            }
            else if (task is FinaleTask)
            {
                display = finaleActiveText;
            }
            else
            {
                // Fallback to name-based mapping (keeps existing behavior for unknown tasks)
                display = GetDisplayForTaskName(task.TaskName, completed: false);
            }

            text.text += "<br>- " + display;
        }
    }

    // Helper: format an active string with cleaned/total counts while allowing inspector customization.
    // If the provided format contains a placeholder for {0} it will be used. Otherwise the counts will be appended.
    private string FormatWithCounts(string format, string shortText, int cleaned, int total)
    {
        if (string.IsNullOrEmpty(format))
        {
            // fallback to short text + counts
            return $"{shortText} ({cleaned}/{total})";
        }

        // If format contains a {0} placeholder, assume user intends to accept counts via string.Format.
        if (format.Contains("{0"))
        {
            try
            {
                return string.Format(format, cleaned, total);
            }
            catch
            {
                // If formatting fails, fallback to safe representation
                return $"{shortText} ({cleaned}/{total})";
            }
        }
        else
        {
            // no placeholders provided: append counts so the UI still shows progress
            return $"{format} ({cleaned}/{total})";
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
                return completed ? paintingCompletedText : paintingActiveShort;
            case "TrashTask":
                return completed ? trashCompletedText : trashActiveShort;
            case "SinglePaintingFall":
            case "SinglePaintingFallTask":
                return completed ? singlePaintingCompletedText : singlePaintingActiveText;
            case "FinaleTask":
                return completed ? finaleCompletedText : finaleActiveText;
            default:
                // Fallback: if completed show past-tense hint, otherwise show the task name
                return completed ? $"Completed: {taskName}" : taskName;
        }
    }
}
