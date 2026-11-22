using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class ThrowMeterUI : MonoBehaviour
{
    private PlayerPickupController _pickupController;

    [SerializeField, Required] private Slider _throwMeterSlider;

    private void Awake()
    {
        _pickupController = FindAnyObjectByType<PlayerPickupController>();
    }

    private void Update()
    {
        _throwMeterSlider.gameObject.SetActive(_pickupController.IsThrowing);
        
        if (!_pickupController.IsThrowing)
            return;
        
        _throwMeterSlider.value = _pickupController.ThrowHoldTimer / _pickupController.ThrowHoldMaxDuration;
    }
}