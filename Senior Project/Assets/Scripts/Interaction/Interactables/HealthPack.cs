using UnityEngine;

public class HealthPack : MonoBehaviour, IPickupUseValidator
{
    public void Heal(PlayerPickupController pickupController)
    {
        if (!pickupController.TryGetComponent(out Health health))
            return;

        if (health.CurrentHealth >= health.MaxHealth)
            return;
        
        int healAmount = health.MaxHealth - health.CurrentHealth;
        health.Heal(healAmount);

        Destroy(gameObject);
    }

    public bool CanUse(PlayerPickupController pickupController)
    {
        if (!pickupController.TryGetComponent(out Health health))
            return false;
        
        return health.CurrentHealth < health.MaxHealth;
    }
}
