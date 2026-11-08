using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStaminaUI : MonoBehaviour
{
    [SerializeField, Required] private PlayerController _playerController;
    [SerializeField, Required] private Slider _staminaSlider;

    [SerializeField] private bool _hideWhenFull = true;

    private void Update()
    {
        if (_hideWhenFull)
            _staminaSlider.gameObject.SetActive(!(_playerController.CurrentStamina >= _playerController.MaxStamina));
        
        _staminaSlider.value = _playerController.CurrentStamina / _playerController.MaxStamina;
    }
}
