using UnityEngine;
using System.Collections.Generic;

public class HallucinationManager : MonoBehaviour
{
    [Header("Hallucination Type")]
    public HallucinationType hallucinationType;

    private IHallucinationLogic logic;

    private void Awake()
    {
        // Instantiate the correct logic class based on the enum
        switch (hallucinationType)
        {
            case HallucinationType.Creeper:
                logic = new CreeperLogic();
                break;
            case HallucinationType.Shadow:
                
                break;
            // more hallucinations
            default:
                Debug.LogWarning("No logic assigned for hallucination type: " + hallucinationType);
                break;
        }

        logic?.Initialize(this);
    }

    private void Start()
    {
        // If you need to do something in Start for the logic, add a method to the interface and call it here
    }

    private void Update()
    {
        logic?.OnUpdate();
    }

    public void OnPlayerSanityChanged(int sanity)
    {
        logic?.OnPlayerSanityChanged(sanity);
    }

    public void StartHaunt()
    {
        logic?.StartHaunt();
    }

    public void StartAttack()
    {
        logic?.StartAttack();
    }
}

public enum HallucinationType
{
    Creeper,
    Shadow,
    // Add more types as needed
}
