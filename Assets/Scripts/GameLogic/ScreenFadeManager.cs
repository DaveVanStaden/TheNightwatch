using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages screen fade effects for scene transitions.
/// Handles fading to black and loading new scenes.
/// Automatically resets on scene load to prevent black screen issues.
/// </summary>
public class ScreenFadeManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("The UI Image that covers the screen (should be black)")]
    [SerializeField] private Image fadeImage;
    
    [Tooltip("Duration of the fade to black transition")]
    [SerializeField] private float fadeDuration = 2f;
    
    [Tooltip("How long to stay black before loading the next scene")]
    [SerializeField] private float holdDuration = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    
    // Singleton instance
    private static ScreenFadeManager instance;
    public static ScreenFadeManager Instance => instance;
    
    private bool isFading = false;
    
    private void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Ensure fade image exists
        if (fadeImage == null)
        {
            Debug.LogError("[ScreenFade] Fade Image not assigned!");
        }
        
        // Always start with transparent screen
        ResetFade();
    }
    
    private void OnEnable()
    {
        // Subscribe to scene loaded event to ensure screen is clear
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    /// <summary>
    /// Called when a new scene is loaded - ensures screen is not black
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (debugLogs) Debug.Log($"[ScreenFade] Scene loaded: {scene.name} - Resetting fade");
        ResetFade();
    }
    
    /// <summary>
    /// Reset the fade to fully transparent (clear screen)
    /// </summary>
    public void ResetFade()
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(false);
            if (debugLogs) Debug.Log("[ScreenFade] Fade reset to transparent");
        }
    }
    
    /// <summary>
    /// Fade from black to transparent (gradual fade in)
    /// </summary>
    public void FadeFromBlack()
    {
        if (fadeImage != null)
        {
            StartCoroutine(FadeFromBlackCoroutine());
        }
    }
    
    /// <summary>
    /// Coroutine that fades from black to transparent
    /// </summary>
    private IEnumerator FadeFromBlackCoroutine()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            
            if (debugLogs) Debug.Log($"[ScreenFade] Starting fade from black (duration: {fadeDuration}s)");
            
            // Fade from black to transparent
            float elapsed = 0f;
            Color startColor = new Color(0f, 0f, 0f, 1f); // Black
            Color targetColor = new Color(0f, 0f, 0f, 0f); // Transparent
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled time to work when game is paused
                float t = elapsed / fadeDuration;
                fadeImage.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            
            // Ensure fully transparent
            fadeImage.color = targetColor;
            fadeImage.gameObject.SetActive(false);
            if (debugLogs) Debug.Log("[ScreenFade] Fade from black complete");
        }
    }
    
    /// <summary>
    /// Fade to black and load the specified scene
    /// </summary>
    /// <param name="sceneIndex">Build index of the scene to load (-1 to just fade without loading)</param>
    public void FadeToBlackAndLoadScene(int sceneIndex)
    {
        if (isFading)
        {
            if (debugLogs) Debug.LogWarning("[ScreenFade] Already fading, ignoring request");
            return;
        }
        
        StartCoroutine(FadeToBlackCoroutine(sceneIndex));
    }
    
    /// <summary>
    /// Fade to black and load the specified scene by name
    /// </summary>
    /// <param name="sceneName">Name of the scene to load</param>
    public void FadeToBlackAndLoadScene(string sceneName)
    {
        if (isFading)
        {
            if (debugLogs) Debug.LogWarning("[ScreenFade] Already fading, ignoring request");
            return;
        }
        
        StartCoroutine(FadeToBlackCoroutine(sceneName));
    }
    
    /// <summary>
    /// Coroutine that handles fade to black and scene loading (by index)
    /// </summary>
    private IEnumerator FadeToBlackCoroutine(int sceneIndex)
    {
        isFading = true;
        
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            
            if (debugLogs) Debug.Log($"[ScreenFade] Starting fade to black (duration: {fadeDuration}s)");
            
            // Fade to black
            float elapsed = 0f;
            Color startColor = fadeImage.color;
            Color targetColor = new Color(0f, 0f, 0f, 1f);
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled time to work when paused
                float t = elapsed / fadeDuration;
                fadeImage.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            
            // Ensure fully black
            fadeImage.color = targetColor;
            if (debugLogs) Debug.Log("[ScreenFade] Fade to black complete");
            
            // Hold on black screen
            if (holdDuration > 0f)
            {
                if (debugLogs) Debug.Log($"[ScreenFade] Holding black screen for {holdDuration}s");
                yield return new WaitForSecondsRealtime(holdDuration); // Use realtime for paused game
            }
            
            // Load the scene only if sceneIndex is valid (>= 0)
            if (sceneIndex >= 0)
            {
                if (debugLogs) Debug.Log($"[ScreenFade] Loading scene index: {sceneIndex}");
                SceneManager.LoadScene(sceneIndex);
            }
            else
            {
                if (debugLogs) Debug.Log("[ScreenFade] No scene load requested (sceneIndex < 0)");
            }
        }
        else
        {
            // Only load scene if valid
            if (sceneIndex >= 0)
            {
                Debug.LogError("[ScreenFade] Fade image is null, loading scene immediately");
                SceneManager.LoadScene(sceneIndex);
            }
        }
        
        isFading = false;
    }
    
    /// <summary>
    /// Coroutine that handles fade to black and scene loading (by name)
    /// </summary>
    private IEnumerator FadeToBlackCoroutine(string sceneName)
    {
        isFading = true;
        
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            
            if (debugLogs) Debug.Log($"[ScreenFade] Starting fade to black (duration: {fadeDuration}s)");
            
            // Fade to black
            float elapsed = 0f;
            Color startColor = fadeImage.color;
            Color targetColor = new Color(0f, 0f, 0f, 1f);
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled time to work when paused
                float t = elapsed / fadeDuration;
                fadeImage.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            
            // Ensure fully black
            fadeImage.color = targetColor;
            if (debugLogs) Debug.Log("[ScreenFade] Fade to black complete");
            
            // Hold on black screen
            if (holdDuration > 0f)
            {
                if (debugLogs) Debug.Log($"[ScreenFade] Holding black screen for {holdDuration}s");
                yield return new WaitForSecondsRealtime(holdDuration); // Use realtime for paused game
            }
            
            // Load the scene
            if (debugLogs) Debug.Log($"[ScreenFade] Loading scene: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("[ScreenFade] Fade image is null, loading scene immediately");
            SceneManager.LoadScene(sceneName);
        }
        
        isFading = false;
    }
    
    /// <summary>
    /// Static helper to trigger fade from anywhere
    /// </summary>
    public static void FadeAndLoadScene(int sceneIndex)
    {
        if (Instance != null)
        {
            Instance.FadeToBlackAndLoadScene(sceneIndex);
        }
        else
        {
            Debug.LogError("[ScreenFade] No ScreenFadeManager instance found! Loading scene directly.");
            SceneManager.LoadScene(sceneIndex);
        }
    }
    
    /// <summary>
    /// Static helper to trigger fade from anywhere (by scene name)
    /// </summary>
    public static void FadeAndLoadScene(string sceneName)
    {
        if (Instance != null)
        {
            Instance.FadeToBlackAndLoadScene(sceneName);
        }
        else
        {
            Debug.LogError("[ScreenFade] No ScreenFadeManager instance found! Loading scene directly.");
            SceneManager.LoadScene(sceneName);
        }
    }
}
