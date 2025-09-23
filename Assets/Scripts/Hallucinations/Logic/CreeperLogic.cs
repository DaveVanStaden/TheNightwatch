public class CreeperLogic : IHallucinationLogic
{
    private HallucinationManager manager;

    public void Initialize(HallucinationManager manager)
    {
        this.manager = manager;
        
    }

    public void OnUpdate()
    {
        
    }

    public void OnPlayerSanityChanged(int sanity)
    {
        
    }

    public void StartHaunt() { /* ... */ }
    public void StartAttack() { /* ... */ }
}