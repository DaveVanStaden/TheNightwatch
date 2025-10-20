using UnityEngine;

public interface IInteraction
{
    // Called when the interaction starts
    void EnterInteraction(PlayerManager playerManager);

    // Called every frame while interacting (e.g., from Update in a manager)
    void UpdateInteraction(PlayerManager playerManager);

    // Called when the interaction ends
    void LeaveInteraction(PlayerManager playerManager);
}