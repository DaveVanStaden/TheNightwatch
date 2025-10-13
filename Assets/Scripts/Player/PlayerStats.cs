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

    public int HP
    {
        get => hp;
        set
        {
            hp = Mathf.Clamp(value, 0, maxHP);
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
        DontDestroyOnLoad(gameObject);

        HP = maxHP;
        Sanity = maxSanity;
        Tiredness = 0;
    }

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
}