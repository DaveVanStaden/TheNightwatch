public interface IHallucinationLogic
{
    void Initialize(HallucinationManager manager);
    void OnUpdate();
    void OnPlayerSanityChanged(int sanity);
    void StartHaunt();
    void StartAttack();
}
