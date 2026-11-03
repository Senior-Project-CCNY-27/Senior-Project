using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStaminaUI : MonoBehaviour
{
    [SerializeField, Required] private PlayerController _playerController;
    [SerializeField, Required] private Slider _staminaSlider;

    private void Update()
    {
        _staminaSlider.value = _playerController.CurrentStamina / _playerController.MaxStamina;
    }
}
