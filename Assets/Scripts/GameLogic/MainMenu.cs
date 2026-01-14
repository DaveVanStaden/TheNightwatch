using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple main menu controller.
/// Hook StartGame() and ExitGame() to UI Buttons in the inspector.
/// Scene 0 = Main Menu, Scene 1 = Game, Scene 3 = Day Mode.
/// Checks PlayerPrefs for game completion to unlock Day Mode button.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene Indices")]
    [Tooltip("Build index of the game scene. Default = 1.")]
    public int gameSceneIndex = 1;
    
    [Tooltip("Build index of the day mode scene. Default = 3.")]
    public int dayModeSceneIndex = 3;
    
    [Header("UI References")]
    [Tooltip("Button or GameObject to show/hide when game is completed (Day Mode button)")]
    public GameObject dayModeButton;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    
    private void Start()
    {
        // Check if player has completed the game
        CheckGameCompletion();
    }
    
    /// <summary>
    /// Check PlayerPrefs to see if game has been completed and show/hide day mode button
    /// </summary>
    private void CheckGameCompletion()
    {
        bool gameCompleted = PlayerPrefs.GetInt("GameCompleted", 0) == 1;
        
        if (debugLogs)
        {
            Debug.Log($"[MainMenu] Game completed: {gameCompleted}");
        }
        
        // Show or hide day mode button based on completion status
        if (dayModeButton != null)
        {
            dayModeButton.SetActive(gameCompleted);
            
            if (debugLogs)
            {
                Debug.Log($"[MainMenu] Day Mode button set to: {(gameCompleted ? "VISIBLE" : "HIDDEN")}");
            }
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[MainMenu] Day Mode button reference not assigned in inspector!");
        }
    }
    
    /// <summary>
    /// Start the normal night mode game
    /// </summary>
    public void StartGame()
    {
        Time.timeScale = 1f; // ensure time scale is normal
        
        if (debugLogs) Debug.Log($"[MainMenu] Starting night mode game (Scene {gameSceneIndex})");
        
        SceneManager.LoadScene(gameSceneIndex);
    }
    
    /// <summary>
    /// Start the day mode game (only available after completing night mode)
    /// </summary>
    public void StartDayMode()
    {
        Time.timeScale = 1f; // ensure time scale is normal
        
        if (debugLogs) Debug.Log($"[MainMenu] Starting day mode game (Scene {dayModeSceneIndex})");
        
        SceneManager.LoadScene(dayModeSceneIndex);
    }

    /// <summary>
    /// Exit the application
    /// </summary>
    public void ExitGame()
    {
        if (debugLogs) Debug.Log("[MainMenu] ExitGame called.");
        
        Application.Quit();
        
#if UNITY_EDITOR
        // In editor, stop play mode
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    
    /// <summary>
    /// Reset game completion (for testing purposes)
    /// Call this from a debug button or context menu
    /// </summary>
    [ContextMenu("DEBUG: Reset Game Completion")]
    public void ResetGameCompletion()
    {
        PlayerPrefs.SetInt("GameCompleted", 0);
        PlayerPrefs.Save();
        
        Debug.Log("[MainMenu] Game completion reset! Day Mode locked again.");
        
        // Refresh the button state
        CheckGameCompletion();
    }
    
    /// <summary>
    /// Unlock day mode (for testing purposes)
    /// Call this from a debug button or context menu
    /// </summary>
    [ContextMenu("DEBUG: Unlock Day Mode")]
    public void UnlockDayMode()
    {
        PlayerPrefs.SetInt("GameCompleted", 1);
        PlayerPrefs.Save();
        
        Debug.Log("[MainMenu] Day Mode unlocked!");
        
        // Refresh the button state
        CheckGameCompletion();
    }
}
