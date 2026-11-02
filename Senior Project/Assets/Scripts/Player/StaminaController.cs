using UnityEngine;
using UnityEngine.UI;

public class StaminaController : MonoBehaviour
{
    [Header("Stamina Main Parameters")]
    public float _playerStamina = 100f;
    [SerializeField] private float _maxStamina = 100f;
    //Need to check if the designers want to sprint with any amount of stamina or only when stamina is full
    //[SerializeField] private float _currentStamina;
    [HideInInspector] public bool _hasRegenerated = true;
    [HideInInspector] public bool _weAreSprinting = false;
    //public Slider staminaUI;

    [Header("Stamina Regen Parameters")]
    [Range(0, 50)][SerializeField] private float _drainRate = 0.5f;
    [Range(0, 50)][SerializeField] private float _regenRate = 0.5f;


    [Header("Stamina Speed Parameters")]
    [SerializeField] private float _normalRunSpeed = 4.5f;

    [Header("Stamina UI Elements")]
    [SerializeField] private Image _staminaProgressUI = null;
    [SerializeField] private CanvasGroup _sliderCanvasGroup = null;


    private PlayerController _playerController;


    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }


    private void Update()
    {
        if(!_weAreSprinting)
        {
            if(_playerStamina <= _maxStamina - 0.01)
            {
                _playerStamina += _regenRate * Time.deltaTime;
                updateStamina(1);

                if(_playerStamina >= _maxStamina)
                {
                    //Set to normal speed
                    _sliderCanvasGroup.alpha = 0;
                    _hasRegenerated = true;
                }
            }
        }
    }

    public void Sprinting()
    {
        if(_hasRegenerated)
        {
            _weAreSprinting = true;
            _playerStamina -= _drainRate * Time.deltaTime;
            updateStamina(1);

            if(_playerStamina <= 0)
            {
                _hasRegenerated = false;
                _sliderCanvasGroup.alpha = 0;
            }
        }
    }

    void updateStamina (float value)
    {
        //Do we need this even though it's a float?
        //_staminaProgressUI.fillAmount = _playerStamina / _maxStamina;

        if (value == 0)
        {
            _sliderCanvasGroup.alpha = 0f;
        }
        else
        {
            _sliderCanvasGroup.alpha = 1f;
        }
    }
}
