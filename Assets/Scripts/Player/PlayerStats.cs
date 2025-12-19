using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Player Stats")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private int maxSanity = 100;
    [SerializeField] private int maxTiredness = 100;

    [SerializeField] private int hp;
    [SerializeField] private int sanity;
    [SerializeField] private int tiredness;

    private float sanityAccumulator = 0f;

    [Header("Inventory / Keys")]
    [Tooltip("Runtime list of key identifiers the player currently holds.")]
    [SerializeField] private List<string> keys = new List<string>();

    [Header("Death / UI")]
    [Tooltip("Assign the Loss screen GameObject (disabled by default). Shown and time is frozen when HP hits 0).")]
    [SerializeField] private GameObject lossScreen;

    // Internal death flag so we only run death logic once
    private bool isDead = false;

    // Backwards-compatible convenience property: true if player has any key.
    public bool HasKey
    {
        get => keys != null && keys.Count > 0;
        // Setting to false clears keys (best-effort compatibility with existing code that sets HasKey = false)
        set
        {
            if (!value && keys != null) keys.Clear();
        }
    }

    public int HP
    {
        get => hp;
        set
        {
            hp = Mathf.Clamp(value, 0, maxHP);
            if (hp <= 0 && !isDead)
            {
                HandleDeath();
            }
        }
    }

    public int Sanity
    {
        get => sanity;
        set
        {
            sanity = Mathf.Clamp(value, 0, maxSanity);
            Debug.Log("Sanity property set to: " + sanity);
        }
    }

    public int Tiredness
    {
        get => tiredness;
        set
        {
            tiredness = Mathf.Clamp(value, 0, maxTiredness);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HP = maxHP;
        Sanity = maxSanity;
        Tiredness = 0;

        isDead = false;

        if (lossScreen != null)
            lossScreen.SetActive(false);
    }

    // Key inventory API
    public void AddKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return;
        if (keys == null) keys = new List<string>();
        if (!keys.Contains(keyId))
            keys.Add(keyId);
    }

    // Renamed to avoid collision with the HasKey property
    public bool HasKeyId(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return false;
        return keys != null && keys.Contains(keyId);
    }

    public bool RemoveKey(string keyId)
    {
        if (string.IsNullOrEmpty(keyId) || keys == null) return false;
        return keys.Remove(keyId);
    }

    public IReadOnlyList<string> GetKeys() => keys.AsReadOnly();

    public void ChangeHP(int amount)
    {
        HP += amount;
    }

    public void ChangeSanity(float amount)
    {
        sanityAccumulator += amount;
        int delta = Mathf.FloorToInt(sanityAccumulator);
        if (delta != 0)
        {
            Sanity = Mathf.Clamp(sanity + delta, 0, maxSanity);
            sanityAccumulator -= delta;
            Debug.Log("Sanity changed to: " + sanity + ", delta: " + delta);
        }
    }

    public void ChangeTiredness(int amount)
    {
        Tiredness += amount;
    }

    // Called once when HP reaches zero
    private void HandleDeath()
    {
        isDead = true;
        Debug.Log("[PlayerStats] Player died. Triggering loss screen and pausing time.");

        // Freeze game time
        Time.timeScale = 0f;

        // Show loss UI if assigned
        if (lossScreen != null)
        {
            lossScreen.SetActive(true);
        }

        // Make cursor visible/unlocked so player can interact with the loss menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Optional helper to reset death state (useful for testing / play again)
    public void ResetDeathState()
    {
        if (!isDead) return;
        isDead = false;
        if (lossScreen != null)
            lossScreen.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}