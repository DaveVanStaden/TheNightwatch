using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple entity that can sit at a camera spawn. It remains inactive until Show() is called.
/// When visible and conditions are met it drains player sanity.
/// </summary>
public class CameraEntity : MonoBehaviour
{
    [Tooltip("Sanity per second drained while player is looking at the camera group and angle condition is met.")]
    public float sanityDrainPerSecond = 5f;

    [Tooltip("Optional event invoked once the entity becomes visible/active.")]
    public UnityEvent onActivated;

    // Filled by the spawner/module
    [HideInInspector] public EventManager manager;
    [HideInInspector] public CamGroup ownerGroup;

    // internal
    bool isVisible = false;

    public void Init(EventManager manager, CamGroup ownerGroup)
    {
        this.manager = manager;
        this.ownerGroup = ownerGroup;
        // remain inactive until group is selected
        gameObject.SetActive(false);
        isVisible = false;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        isVisible = true;
        onActivated?.Invoke();
    }

    private void Update()
    {
        if (!isVisible) return;
        if (manager == null) return;

        // Only drain when player is in interaction view, owner group is selected, and the active Interactable's angle == 2
        if (manager.playerManager != null && manager.playerManager.inInteractionView)
        {
            // Find the currently active Interactable (interactionCamera enabled)
            Interactable active = null;
            var all = FindObjectsOfType<Interactable>();
            for (int i = 0; i < all.Length; i++)
            {
                var it = all[i];
                if (it.interactionCamera != null && it.interactionCamera.enabled)
                {
                    active = it;
                    break;
                }
            }

            if (active != null && active.currentAngle == 2 && ownerGroup != null && ownerGroup.selectedGroup)
            {
                // drain sanity
                if (PlayerStats.Instance != null)
                {
                    PlayerStats.Instance.ChangeSanity(-sanityDrainPerSecond * Time.deltaTime);
                }
            }
        }
    }
}
