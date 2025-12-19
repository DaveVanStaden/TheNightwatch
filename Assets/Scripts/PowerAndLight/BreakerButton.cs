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
    [Tooltip("Optional audio settings to play when toggled")]
    [SerializeField] private AudioSource clickAudio;
    [SerializeField] private AudioClip onSound;
    [SerializeField] private AudioClip offSound;
    [Tooltip("Scale when pressed (local scale multiplier)")]
    [SerializeField] private float pressScale = 0.85f;
    [Tooltip("Time for press / release animation")]
    [SerializeField] private float pressDuration = 0.08f;
    [Tooltip("Optional renderer to flash color when toggled")]
    [SerializeField] private Renderer feedbackRenderer;
    [SerializeField] private Color feedbackColor = Color.yellow;
    [SerializeField] private float feedbackColorTime = 0.12f;

    [Header("PowerGroup Light")]
    [Tooltip("Optional Unity Light that indicates the PowerGroup state (enabled = powered)")]
    [SerializeField] private Light groupLight;
    [Tooltip("Optional Renderer (e.g. a mesh with emissive material) that indicates the PowerGroup state")]
    [SerializeField] private Renderer groupLightRenderer;

    [Header("PowerGroup Materials (optional)")]
    [Tooltip("Material to use when the PowerGroup is powered")]
    [SerializeField] private Material groupOnMaterial;
    [Tooltip("Material to use when the PowerGroup is unpowered")]
    [SerializeField] private Material groupOffMaterial;

    [Header("Animator")]
    [Tooltip("Animator to use when toggling the switch")]
    [SerializeField] Animator animator;

    [Header("Behavior")]
    [Tooltip("If true this switch remains interactable even when global power is out (use for main power lever).")]
    [SerializeField] private bool ignoreGlobalPowerLock = false;

    [Header("Events")]
    public UnityEvent<bool> onToggled; // bool = new state

    public bool isOn { get; private set; }

    // cached original values for feedbackRenderer
    private Vector3 originalScale;
    private Color[] originalColors;
    private Material[] instanceMaterials;

    // cached instance materials for groupLightRenderer
    private Material[] groupInstanceMaterials;

    // cached collider for enabling/disabling interaction when global power changes
    private Collider cachedCollider;

    private void Awake()
    {
        originalScale = transform.localScale;
        isOn = startsOn;
        cachedCollider = GetComponent<Collider>();

        CacheAndInstanceMaterials();
        CacheAndInstanceGroupMaterials();

        // if global power is already out at startup, disable interaction unless this switch ignores the global lock
        var elec = Object.FindAnyObjectByType<ElectricityLogic>();
        if (elec != null && elec.IsPowerOut && cachedCollider != null && !ignoreGlobalPowerLock)
        {
            cachedCollider.enabled = false;
        }

        ApplyState(initial: true);
    }

    private void OnEnable()
    {
        // Subscribe to global power events so this button becomes unavailable when power is out,
        // and re-enabled when power is restored.
        var elec = Object.FindAnyObjectByType<ElectricityLogic>();
        if (elec != null)
        {
            elec.onPowerOut.AddListener(OnGlobalPowerOut);
            elec.onPowerRestored.AddListener(OnGlobalPowerRestored);
        }
    }

    private void OnDisable()
    {
        var elec = Object.FindAnyObjectByType<ElectricityLogic>();
        if (elec != null)
        {
            elec.onPowerOut.RemoveListener(OnGlobalPowerOut);
            elec.onPowerRestored.RemoveListener(OnGlobalPowerRestored);
        }
    }

    private void OnGlobalPowerOut()
    {
        // Make this switch unavailable for interaction unless it's explicitly allowed to ignore the global lock.
        if (cachedCollider != null && !ignoreGlobalPowerLock) cachedCollider.enabled = false;

        // If this is the main power switch (ignoreGlobalPowerLock==true) and it's currently ON,
        // toggle it OFF so the lever visually reflects the global power loss.
        // Use Toggle() so audio/animation/visual feedback run as normal.
        if (ignoreGlobalPowerLock && isOn)
        {
            // Toggle will run even if collider is enabled; for main switch collider stays enabled.
            Toggle();
            // Toggle already calls ApplyState, audio, animation and onToggled.
            // We early-return to avoid calling ApplyState twice.
            return;
        }

        ApplyState();
    }

    private void OnGlobalPowerRestored()
    {
        // Re-enable interaction and re-apply state (turn group on if this switch is on).
        if (cachedCollider != null) cachedCollider.enabled = true;
        ApplyState();
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

    private void CacheAndInstanceGroupMaterials()
    {
        if (groupLightRenderer == null) return;
        var mats = groupLightRenderer.materials;
        groupInstanceMaterials = new Material[mats.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            groupInstanceMaterials[i] = new Material(mats[i]);
        }
        groupLightRenderer.materials = groupInstanceMaterials;
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"BreakerButton '{name}' has no Collider — add one so it can be clicked (BoxCollider, MeshCollider, etc.).", this);
        }
    }

    private void ApplyState(bool initial = false)
    {
        // Respect global power: PowerGroups should already be handling global power via ElectricityLogic subscriptions.
        if (powerGroup != null)
        {
            // When global power is out PowerGroups will force-off; when available, respect this button's isOn.
            if (isOn)
                powerGroup.TurnOnLights();
            else
                powerGroup.TurnOffLights();
        }

        bool groupActive = powerGroup != null ? powerGroup.AnyLightOn() : isOn;
        UpdateGroupLightState(groupActive, instantly: initial);

        if (initial)
        {
            UpdateRendererColor(isOn ? feedbackColor : Color.white, instantly: true);
        }
    }

    public void Toggle()
    {
        // allow toggling if collider enabled OR if this button ignores the global lock
        if (cachedCollider != null && !cachedCollider.enabled && !ignoreGlobalPowerLock)
            return;

        isOn = !isOn;
        Debug.Log($"[BreakerButton] '{name}' toggled -> {isOn}");

        ApplyState();

        if (clickAudio != null)
        {
            clickAudio.clip = isOn ? onSound : offSound;
            clickAudio.pitch = Random.Range(0.95f, 1.05f);
            clickAudio.PlayOneShot(clickAudio.clip);
        }

        StopAllCoroutines();
        StartCoroutine(PressAnimation());
        if (feedbackRenderer != null)
            StartCoroutine(FlashColorCoroutine());

        onToggled?.Invoke(isOn);
        ToggleAnimation();
    }

    private IEnumerator PressAnimation()
    {
        Vector3 pressed = originalScale * pressScale;
        float half = pressDuration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, pressed, t / half);
            yield return null;
        }
        transform.localScale = pressed;
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

        for (int i = 0; i < instanceMaterials.Length; i++)
        {
            if (instanceMaterials[i].HasProperty("_Color"))
                instanceMaterials[i].color = originalColors[i];
        }
    }

    private void UpdateGroupLightState(bool on, bool instantly = false)
    {
        if (groupLightRenderer != null && groupOnMaterial != null && groupOffMaterial != null)
        {
            Material src = on ? groupOnMaterial : groupOffMaterial;
            var newMats = new Material[groupLightRenderer.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = new Material(src);
            groupLightRenderer.materials = newMats;
        }
        else
        {
            if (groupLight != null)
                groupLight.enabled = on;
        }

        if (groupLight != null)
            groupLight.enabled = on;
    }

    public void OnPressed()
    {
        Toggle();
    }

    private void UpdateRendererColor(Color color, bool instantly = false)
    {
        if (instanceMaterials == null || instanceMaterials.Length == 0) return;
        for (int i = 0; i < instanceMaterials.Length; i++)
        {
            if (instanceMaterials[i].HasProperty("_Color"))
                instanceMaterials[i].color = color;
        }
    }

    public void ToggleAnimation()
    {
        if (animator == null) return;
        try
        {
            // legacy behavior kept: animator boolean parameter named same as GameObject used previously
            animator.SetBool(name, isOn);
        }
        catch
        {
            // swallow any animator errors to avoid spamming console
        }
    }
}