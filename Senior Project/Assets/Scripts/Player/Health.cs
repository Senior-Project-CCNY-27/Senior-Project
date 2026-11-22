using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class Health : MonoBehaviour
{
    [field: SerializeField, ReadOnly] public int CurrentHealth { get; private set; }
    [field: SerializeField] public int MaxHealth { get; private set; } = 1;
    [field: SerializeField, ReadOnly] public bool IsDead { get; private set; }

    [field: Header("Events")]
    [field: SerializeField] public UnityEvent<int, int> OnHealthChanged { get; private set; } = new(); // prevHealth, currHealth
    [field: SerializeField] public UnityEvent<int> OnDamageTaken { get; private set; } = new();
    [field: SerializeField] public UnityEvent<int> OnHeal { get; private set; } = new();
    [field: SerializeField] public UnityEvent OnDeath { get; private set; } = new();

    private void Awake()
    {
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead)
            return;

        if (amount <= 0)
            return;
        
        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, MaxHealth);
        
        OnHealthChanged?.Invoke(previousHealth, CurrentHealth);
        OnDamageTaken?.Invoke(CurrentHealth - previousHealth);

        if (CurrentHealth == 0)
        {
            IsDead = true;
            OnDeath.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead)
            return;
        
        if (amount <= 0)
            return;
        
        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0, MaxHealth);
        
        OnHealthChanged.Invoke(previousHealth, CurrentHealth);
        OnHeal?.Invoke(CurrentHealth - previousHealth);
    }
}