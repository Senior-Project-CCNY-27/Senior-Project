using System;
using DG.Tweening;
using UnityEngine;

public class CameraFOVController : MonoBehaviour
{
    [SerializeField] private PlayerController _player;
    [SerializeField] private Camera _camera;
    
    [Header("Sprinting")]
    [SerializeField] private float _sprintFOV = 85f;
    [SerializeField] private float _adjustmentDuration = 0.75f;
    [SerializeField] private Ease _adjustmentEase = Ease.OutCubic;
    
    private float _baseFOV;

    private void Awake()
    {
        _baseFOV = _camera.fieldOfView;
    }

    private void OnEnable()
    {
        _player.OnStateChanged += Player_OnStateChanged;
    }

    private void OnDisable()
    {
        _player.OnStateChanged -= Player_OnStateChanged;
    }
    
    private void Player_OnStateChanged(PlayerController.State newState, PlayerController.State previousState)
    {
        if (previousState == PlayerController.State.Sprint)
        {
            ChangeFOV(_baseFOV);
            return;
        }

        if (newState == PlayerController.State.Sprint)
        {
            ChangeFOV(_sprintFOV);
            return;
        }
    }

    private void ChangeFOV(float newFOV)
    {
        _camera.DOKill();
        _camera.DOFieldOfView(newFOV, _adjustmentDuration).SetEase(_adjustmentEase);
    }
}