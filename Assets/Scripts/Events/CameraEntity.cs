using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple entity that can sit at a camera spawn. It remains inactive until Show() is called.
/// When visible it drains player sanity and notifies the module when destroyed.
/// </summary>
public class CameraEntity : MonoBehaviour
{
    [Tooltip("Sanity per second drained while the entity is visible.")]
    public float sanityDrainPerSecond = 5f;

    [Tooltip("Optional event invoked once the entity becomes visible/active.")]
    public UnityEvent onActivated;

    // Filled by the spawner/module
    [HideInInspector] public EventManager manager;
    [HideInInspector] public CamGroup ownerGroup;

    // internal
    private bool isVisible = false;

    // Notifies spawner when this entity disables/destroys
    public event Action onDestroyed;

    public void Init(EventManager manager, CamGroup ownerGroup)
    {
        this.manager = manager;
        this.ownerGroup = ownerGroup;
        // remain inactive until Show() is called
        gameObject.SetActive(false);
        isVisible = false;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        isVisible = true;
        onActivated?.Invoke();
    }

    /// <summary>
    /// Safe API to request this entity be removed by its own logic.
    /// Use instead of external Destroy(...) when possible.
    /// </summary>
    public void Kill()
    {
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!isVisible) return;

        bool playerHasManager = manager != null && manager.playerManager != null;
        bool playerOnCams = playerHasManager && manager.playerManager.inInteractionView;
        bool ownerSelected = ownerGroup != null && ownerGroup.selectedGroup;

        if (manager == null)
        {
            Destroy(gameObject);
            return;
        }

        // If player left the camera UI or group deselected -> self-destruct
        if (!playerOnCams || ownerGroup == null || !ownerSelected)
        {
            Destroy(gameObject);
            return;
        }

        // Drain sanity while visible
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ChangeSanity(-sanityDrainPerSecond * Time.deltaTime);
        }
    }

    private void OnDisable()
    {
        // Mark invisible and notify subscribers
        isVisible = false;
        try
        {
            onDestroyed?.Invoke();
        }
        catch (Exception) { /* ignore subscriber exceptions */ }
    }

    private void OnDestroy()
    {
        // intentionally empty - kept for future cleanup if needed
    }
}
