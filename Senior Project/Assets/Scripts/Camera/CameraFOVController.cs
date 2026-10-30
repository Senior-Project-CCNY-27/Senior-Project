using DG.Tweening;
using NaughtyAttributes;
using Unity.Cinemachine;
using UnityEngine;

public class CameraFOVController : MonoBehaviour
{
    [SerializeField, Required] private PlayerController _player;
    [SerializeField, Required] private CinemachineCamera _camera;
    
    [Header("Sprinting")]
    [SerializeField] private float _sprintFOV = 85f;
    [SerializeField] private float _adjustmentDuration = 0.75f;
    [SerializeField] private Ease _adjustmentEase = Ease.OutCubic;
    
    private float _baseFOV;

    private void Awake()
    {
        _baseFOV = _camera.Lens.FieldOfView;
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
            TweenFOV(_baseFOV);
            return;
        }

        if (newState == PlayerController.State.Sprint)
        {
            TweenFOV(_sprintFOV);
            return;
        }
    }

    private void TweenFOV(float newFOV)
    {
        _camera.DOKill();
        DOVirtual.Float(_camera.Lens.FieldOfView, newFOV, _adjustmentDuration, SetFOV).SetEase(_adjustmentEase).SetId(_camera);
    }

    private void SetFOV(float newFOV)
    {
        _camera.Lens.FieldOfView = newFOV;
    }
}