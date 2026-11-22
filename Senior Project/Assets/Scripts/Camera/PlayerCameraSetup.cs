using System;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

[ExecuteAlways]
public class PlayerCameraSetup : MonoBehaviour
{
    [SerializeField, Required] private CinemachineCamera _camera;
    private void Update()
    {
        FindTargetIfMissing();
    }

    private void OnValidate()
    {
        FindTargetIfMissing();
    }

    private void FindTargetIfMissing()
    {
        if (_camera == null || _camera.Target.TrackingTarget != null)
            return;

        CameraTarget target = FindAnyObjectByType<CameraTarget>();
        if (target == null)
            return;
        
        _camera.Target.TrackingTarget = target.transform;
    }
}
