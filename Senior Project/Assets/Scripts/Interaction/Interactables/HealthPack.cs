using UnityEngine;
using UnityEngine.InputSystem;

public class HealthPack : MonoBehaviour
{
    private Health _health;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       _health = FindFirstObjectByType<Health>();
    }

    public void Heal()
    {
        if(_health.CurrentHealth < _health.MaxHealth)
        {
            int healAmount = _health.MaxHealth - _health.CurrentHealth;

            _health.Heal(healAmount);

            Destroy(gameObject);
        }
    }
}
