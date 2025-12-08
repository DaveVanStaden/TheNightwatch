using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple main menu controller.
/// Hook StartGame() and ExitGame() to UI Buttons in the inspector.
/// Scene 0 = Main Menu, Scene 1 = Game.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Build index of the game scene. Default = 1.")]
    public int gameSceneIndex = 1;
    public void StartGame()
    {
        Time.timeScale = 1f; // ensure time scale is normal
        SceneManager.LoadScene(gameSceneIndex);
    }

    public void ExitGame()
    {
        Debug.Log("[MainMenu] ExitGame called.");
        Application.Quit();
    }
}
