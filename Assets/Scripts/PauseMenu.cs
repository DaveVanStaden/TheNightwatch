using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    bool isPaused = false;
    PlayerManager pm;
    [Tooltip("Build index of the main menu scene. Default = 0.")]
    public int mainMenuSceneIndex = 0;

    [Tooltip("Build index of the game scene to restart. Default = 1.")]
    public int gameSceneIndex = 1;

    [Header("Finished UI")]
    [Tooltip("UI GameObject (panel) that will be activated when escape is pressed. Assign in inspector.")]
    public GameObject finishedScreen;

    MonitorCursor[] cursors;
    MonitorCursor currentCursor;
    private void Awake()
    {
        pm = FindAnyObjectByType<PlayerManager>();
        cursors = FindObjectsByType<MonitorCursor>(sortMode: FindObjectsSortMode.None);
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused)
            {
                Pause();
            }
            else
            {
                Unpause();
            }
        }
    }
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneIndex);
    }

    /// <summary>
    /// Return to the main menu (loads scene at mainMenuSceneIndex).
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneIndex);
    }

    /// <summary>
    /// Optionally allow quitting directly from the end screen.
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("[EndMenu] ExitGame called.");
        Application.Quit();
    }
    public void Pause()
    {
        // Pause game time while end menu is open
        Time.timeScale = 0f;
        pm.PauseCamera();
        // Unlock and show the cursor so the player can interact with the menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[PauseMenu] Opened - time paused and cursor unlocked.");
        finishedScreen.SetActive(true);
        isPaused = true;
        foreach (MonitorCursor cursor in cursors)
        {
            if (cursor.beingControlled)
            {
                currentCursor = cursor;
                currentCursor.DisableCursor();
            }
        }
    }
    public void Unpause()
    {
        Time.timeScale = 1f;
        pm.ResumeCamera();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (currentCursor != null)
        {
            currentCursor.Delay();
            currentCursor = null;
        }

        Debug.Log("[PauseMenu] Closed - time unpaused and cursor locked.");
        finishedScreen.SetActive(false);
        isPaused = false;
    }
}
