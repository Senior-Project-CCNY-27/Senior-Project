using NaughtyAttributes;
using UnityEngine;

public class CameraShaker : MonoBehaviour
{
    [SerializeField] private CameraShakeSettings _settings;

    [Button("Shake", EButtonEnableMode.Playmode)]
    public void Shake()
    {
        CameraShakeManager.ShakeCamera(_settings);
    }
}