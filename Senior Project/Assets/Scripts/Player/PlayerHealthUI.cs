using NaughtyAttributes;
using TMPro;
using UnityEngine;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField, Required] private Health _playerHealth;
    [SerializeField, Required] private TMP_Text _healthText;

    private void Start()
    {
        _playerHealth.OnHealthChanged.AddListener(OnPlayerHealthChanged);
        
        OnPlayerHealthChanged(_playerHealth.MaxHealth, _playerHealth.CurrentHealth);
    }

    private void OnDestroy()
    {
        _playerHealth.OnHealthChanged.RemoveListener(OnPlayerHealthChanged);
    }

    private void OnPlayerHealthChanged(int prevHealth, int newHealth)
    {
        _healthText.text = $"Health: {newHealth}/{_playerHealth.MaxHealth}";
    }
}