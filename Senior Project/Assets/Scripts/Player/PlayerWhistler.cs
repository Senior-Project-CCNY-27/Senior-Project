using System;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerWhistler : MonoBehaviour
{
    [Header("Whistle")]
    [SerializeField] private float _whistleRadius = 10f;
    [SerializeField] private float _whistleCooldown = 5f;
    [SerializeField] private float _volume = 100f;
    
    [SerializeField, ReadOnly] private float _cooldownTimer;

    [SerializeField, Required] private SoundDataScriptableEvent _soundListener;

    private void Awake()
    {
        _cooldownTimer = _whistleCooldown;
    }

    private void OnEnable()
    {
        InputManager.Instance.Actions.Player.Whistle.performed += OnWhistlePerformed;
    }

    private void OnDisable()
    {
        if(InputManager.Instance != null)
            InputManager.Instance.Actions.Player.Whistle.performed -= OnWhistlePerformed;
    }

    private void Update()
    {
        if (_cooldownTimer < _whistleCooldown)
            _cooldownTimer += Time.deltaTime;
    }

    private void OnWhistlePerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (_cooldownTimer < _whistleCooldown)
            return;

        _soundListener.FireEvent(new SoundData()
        {
            Position = transform.position,
            Radius = _whistleRadius,
            Source = gameObject,
            Volume = _volume,
        });
    }
}
