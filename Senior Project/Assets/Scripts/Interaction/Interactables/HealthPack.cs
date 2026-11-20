using UnityEngine;

public class HealthPack : MonoBehaviour
{
    public void Heal(PlayerPickupController playerPickupController)
    {
        if (!playerPickupController.TryGetComponent(out Health health))
            return;

        if (health.CurrentHealth >= health.MaxHealth)
            return;
        
        int healAmount = health.MaxHealth - health.CurrentHealth;
        health.Heal(healAmount);

        Destroy(gameObject);
    }
}
