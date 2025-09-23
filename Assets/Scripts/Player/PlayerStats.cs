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

    public int HP
    {
        get => hp;
        private set => hp = Mathf.Clamp(value, 0, maxHP);
    }

    public int Sanity
    {
        get => sanity;
        private set => sanity = Mathf.Clamp(value, 0, maxSanity);
    }

    public int Tiredness
    {
        get => tiredness;
        private set => tiredness = Mathf.Clamp(value, 0, maxTiredness);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize stats
        HP = maxHP;
        Sanity = maxSanity;
        Tiredness = 0;
    }

    // Methods to modify stats
    public void ChangeHP(int amount) => HP += amount;
    public void ChangeSanity(int amount) => Sanity += amount;
    public void ChangeTiredness(int amount) => Tiredness += amount;

    // Optionally, add methods to reset or set stats directly
    public void SetHP(int value) => HP = value;
    public void SetSanity(int value) => Sanity = value;
    public void SetTiredness(int value) => Tiredness = value;
}