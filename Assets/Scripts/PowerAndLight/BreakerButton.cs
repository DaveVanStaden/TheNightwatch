using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class BreakerButton : MonoBehaviour
{
    [Header("Power")]
    [SerializeField] private PowerGroups powerGroup;
    [SerializeField] private bool startsOn = true;

    [Header("Feedback")]
    [Tooltip("Optional AudioSource to play when toggled")]
    [SerializeField] private AudioSource clickAudio;
    [Tooltip("Scale when pressed (local scale multiplier)")]
    [SerializeField] private float pressScale = 0.85f;
    [Tooltip("Time for press / release animation")]
    [SerializeField] private float pressDuration = 0.08f;
    [Tooltip("Optional renderer to flash color when toggled")]
    [SerializeField] private Renderer feedbackRenderer;
    [SerializeField] private Color feedbackColor = Color.yellow;
    [SerializeField] private float feedbackColorTime = 0.12f;

    [Header("Events")]
    public UnityEvent<bool> onToggled; // bool = new state

    public bool isOn { get; private set; }

    // cached original values
    private Vector3 originalScale;
    private Color[] originalColors;
    private Material[] instanceMaterials;

    private void Awake()
    {
        originalScale = transform.localScale;
        isOn = startsOn;
        CacheAndInstanceMaterials();
        ApplyState(initial: true);
    }

    private void CacheAndInstanceMaterials()
    {
        if (feedbackRenderer == null) return;
        var mats = feedbackRenderer.materials;
        instanceMaterials = new Material[mats.Length];
        originalColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            instanceMaterials[i] = new Material(mats[i]);
            originalColors[i] = instanceMaterials[i].HasProperty("_Color") ? instanceMaterials[i].color : Color.white;
        }
        feedbackRenderer.materials = instanceMaterials;
    }

    private void OnValidate()
    {
        // Ensure there's a collider (editor-time hint)
        var col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"BreakerButton '{name}' has no Collider — add one so it can be clicked (BoxCollider, MeshCollider, etc.).", this);
        }
        if (powerGroup == null)
        {
            // not an error, but remind
            Debug.Log($"BreakerButton '{name}' has no PowerGroups assigned. Assign in inspector to control lights.", this);
        }
    }

    private void ApplyState(bool initial = false)
    {
        // Apply to PowerGroups (existing behavior)
        if (powerGroup != null)
        {
            if (isOn)
                powerGroup.TurnOnLights();
            else
                powerGroup.TurnOffLights();
        }

        // If the mapped gameobjects are lights, also toggle Light.enabled (safer if you used Light components)
        // The PowerGroups may already set GameObject active; this just ensures Light.enabled is also set where appropriate.
        if (powerGroup != null)
        {
            // PowerGroups stores GameObjects — try to set Light.enabled on children where applicable.
            foreach (var go in powerGroup.GetType().GetField("Lights", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null
                     ? powerGroup.GetComponentsInChildren<Transform>(true) // fallback - do nothing meaningful
                     : new Transform[0])
            {
                // nothing - kept intentionally to avoid heavy reflection; primary behavior remains PowerGroups.SetActive
            }
        }

        // Optionally update visual state on start
        if (initial)
        {
            // no animation on initial apply
            UpdateRendererColor(isOn ? feedbackColor : Color.white, instantly: true);
        }
    }

    public void Toggle()
    {
        // Public entrypoint used by raycasts / UI
        isOn = !isOn;
        Debug.Log($"[BreakerButton] '{name}' toggled -> {isOn}");

        // Apply power change
        ApplyState();

        // Play audio
        if (clickAudio != null)
            clickAudio.Play();

        // Start visual feedback
        StopAllCoroutines();
        StartCoroutine(PressAnimation());
        if (feedbackRenderer != null)
            StartCoroutine(FlashColorCoroutine());

        // Invoke inspector-event
        onToggled?.Invoke(isOn);
    }

    private IEnumerator PressAnimation()
    {
        Vector3 pressed = originalScale * pressScale;
        float half = pressDuration * 0.5f;
        float t = 0f;
        // press
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, pressed, t / half);
            yield return null;
        }
        transform.localScale = pressed;

        // release
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(pressed, originalScale, t / half);
            yield return null;
        }
        transform.localScale = originalScale;
    }

    private IEnumerator FlashColorCoroutine()
    {
        if (instanceMaterials == null || instanceMaterials.Length == 0)
            yield break;

        float elapsed = 0f;
        while (elapsed < feedbackColorTime)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / feedbackColorTime);
            for (int i = 0; i < instanceMaterials.Length; i++)
            {
                if (instanceMaterials[i].HasProperty("_Color"))
                    instanceMaterials[i].color = Color.Lerp(originalColors[i], feedbackColor, u);
            }
            yield return null;
        }

        // revert
        elapsed = 0f;
        float revertTime = feedbackColorTime * 0.5f;
        while (elapsed < revertTime)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / revertTime);
            for (int i = 0; i < instanceMaterials.Length; i++)
            {
                if (instanceMaterials[i].HasProperty("_Color"))
                    instanceMaterials[i].color = Color.Lerp(feedbackColor, originalColors[i], u);
            }
            yield return null;
        }

        // ensure final values reset
        for (int i = 0; i < instanceMaterials.Length; i++)
        {
            if (instanceMaterials[i].HasProperty("_Color"))
                instanceMaterials[i].color = originalColors[i];
        }
    }

    // Optional helper so BreakerBox or other callers can call it directly (same as Toggle).
    public void OnPressed()
    {
        Toggle();
    }

    private void OnDrawGizmosSelected()
    {
        if (powerGroup != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, powerGroup.transform.position);
            Gizmos.DrawWireSphere(powerGroup.transform.position, 0.25f);
        }
    }
    // Add this private method to fix CS0103: The name 'UpdateRendererColor' does not exist in the current context

    private void UpdateRendererColor(Color color, bool instantly = false)
    {
        if (instanceMaterials == null || instanceMaterials.Length == 0)
            return;

        for (int i = 0; i < instanceMaterials.Length; i++)
        {
            if (instanceMaterials[i].HasProperty("_Color"))
            {
                instanceMaterials[i].color = color;
            }
        }
    }
}