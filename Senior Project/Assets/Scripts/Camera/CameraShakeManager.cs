using Unity.Cinemachine;
using UnityEngine;

public class CameraShakeManager : Singleton<CameraShakeManager>
{
    private CinemachineCamera _camera;
    CinemachineBasicMultiChannelPerlin _noiseComponent;

    private float _startingAmplitude;
    private float _startingFrequency;
    private float _shakeTimer;
    private float _shakeDuration;

    private protected override void Awake()
    {
        base.Awake();
        
        _camera = GetComponent<CinemachineCamera>();
        _noiseComponent = _camera.GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    public static void ShakeCamera(float amplitude, float frequency, float duration)
    {
        if (Instance == null)
            return;
        
        Instance._noiseComponent.ReSeed();

        Instance._noiseComponent.AmplitudeGain = amplitude;
        Instance._noiseComponent.FrequencyGain = frequency;
        Instance._startingAmplitude = amplitude;
        Instance._startingFrequency = frequency;
        Instance._shakeDuration = duration;
        Instance._shakeTimer = duration;
    }

    public static void ShakeCamera(CameraShakeSettings settings)
    {
        ShakeCamera(settings.Intensity, settings.Speed, settings.Duration);
    }

    private void Update()
    {
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.unscaledDeltaTime;

            _noiseComponent.AmplitudeGain =
                Mathf.Lerp(_startingAmplitude, 0, 1 - (_shakeTimer / _shakeDuration));
            _noiseComponent.FrequencyGain =
                Mathf.Lerp(_startingFrequency, 0, 1 - (_shakeTimer / _shakeDuration));
        }
    }
}

[System.Serializable]
public struct CameraShakeSettings
{
    public float Duration;
    public float Intensity;
    public float Speed;
}