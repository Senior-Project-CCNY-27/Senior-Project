using Sirenix.OdinInspector;
using UnityEngine;

public class CameraShaker : MonoBehaviour
{
    [SerializeField] private CameraShakeSettings _settings;

    [Button("Shake", ButtonSizes.Large)]
    public void Shake()
    {
        CameraShakeManager.ShakeCamera(_settings);
    }
}