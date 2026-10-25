using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [SerializeField, Required] private Interactor _interactor;
    [SerializeField, Required] private Image _crosshairImage;

    [SerializeField] private Color _hasInteractableColor = Color.red;

    private Color _defaultColor;

    private void Awake()
    {
        // Grab default color
        _defaultColor = _crosshairImage.color;
    }

    private void LateUpdate()
    {
        _crosshairImage.color = _interactor.CurrentInteractable == null ? _defaultColor : _hasInteractableColor;
    }
}
