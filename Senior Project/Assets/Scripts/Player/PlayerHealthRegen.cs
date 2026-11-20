using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerHealthRegen : MonoBehaviour
{
    [SerializeField, Required] private Health _health;
    [SerializeField] private float _regenDuration = 5f;
    [SerializeField, ReadOnly] private float _regenTimer;

    private void Update()
    {
        if(_health.IsDead)
            return;

        if(_health.CurrentHealth >= _health.MaxHealth)
        {
            _regenTimer = 0f;
            return;
        }
            
        _regenTimer += Time.deltaTime;
        if(_regenTimer >= _regenDuration)
        {
            _health.Heal(1);
            _regenTimer = 0f;
        }
    }
}
