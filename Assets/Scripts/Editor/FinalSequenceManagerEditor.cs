using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Custom inspector for FinalSequenceManager to show helpful status info
/// </summary>
[CustomEditor(typeof(FinalSequenceManager))]
public class FinalSequenceManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FinalSequenceManager manager = (FinalSequenceManager)target;

        // Status box
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Final Sequence Status", MessageType.Info);
        
        EditorGUILayout.LabelField("Sequence State", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Activated:", manager.sequenceActivated ? "YES ✓" : "NO");
        EditorGUILayout.LabelField("Chase Active:", manager.chaseActive ? "YES ✓" : "NO");
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        // Task completion status
        var taskManager = FindAnyObjectByType<TaskManager>();
        if (taskManager != null)
        {
            EditorGUILayout.LabelField("Task Completion", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            bool tutorialComplete = taskManager.completedTasks.Contains("SinglePaintingFallTask") || 
                                    taskManager.completedTasks.Contains("SinglePaintingFall");
            bool paintingComplete = taskManager.completedTasks.Contains("PaintingTask");
            bool trashComplete = taskManager.completedTasks.Contains("TrashTask");

            EditorGUILayout.LabelField("Tutorial Task:", tutorialComplete ? "✓ Complete" : "✗ Incomplete");
            EditorGUILayout.LabelField("Painting Task:", paintingComplete ? "✓ Complete" : "✗ Incomplete");
            EditorGUILayout.LabelField("Trash Task:", trashComplete ? "✓ Complete" : "✗ Incomplete");

            int completedCount = (tutorialComplete ? 1 : 0) + (paintingComplete ? 1 : 0) + (trashComplete ? 1 : 0);
            EditorGUILayout.LabelField("Progress:", $"{completedCount}/3 tasks complete");

            // Show all completed task names for debugging
            if (taskManager.completedTasks.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Completed Tasks List:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                foreach (var task in taskManager.completedTasks)
                {
                    EditorGUILayout.LabelField($"• {task}", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        // Warning boxes
        if (manager.sequenceActivated)
        {
            EditorGUILayout.HelpBox("⚠ Final Sequence is ACTIVE!", MessageType.Warning);
        }

        if (manager.chaseActive)
        {
            EditorGUILayout.HelpBox("🔴 CHASE MODE ACTIVE - Painter is hunting!", MessageType.Error);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // Draw default inspector
        DrawDefaultInspector();
    }
}
#endif
