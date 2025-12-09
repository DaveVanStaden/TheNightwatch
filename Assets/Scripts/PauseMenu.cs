using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{

    [Tooltip("Build index of the main menu scene. Default = 0.")]
    public int mainMenuSceneIndex = 0;

    [Tooltip("Build index of the game scene to restart. Default = 1.")]
    public int gameSceneIndex = 1;

    private void OnEnable()
    {
        // Pause game time while end menu is open
        Time.timeScale = 0f;

        // Unlock and show the cursor so the player can interact with the menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[EndMenu] Opened - time paused and cursor unlocked.");
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            enabled = true;
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
}
