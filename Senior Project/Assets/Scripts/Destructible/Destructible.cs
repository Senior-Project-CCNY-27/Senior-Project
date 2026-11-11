using System;
using MonsterStates;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class Destructible : MonoBehaviour
{
    [SerializeField, Required] private BoolScriptableEvent _onDestructibleEnabledEvent;
    private NavMeshObstacle _navMeshObstacle;
    
    [Header("Camera Shake")]
    [SerializeField] private CameraShakeSettings _shakeSettings;

    private void Awake()
    {
        _navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    private void OnEnable()
    {
        _onDestructibleEnabledEvent.OnEventFired += OnDestructibleEnabledEvent;
    }

    private void OnDisable()
    {
        if(_onDestructibleEnabledEvent != null)
            _onDestructibleEnabledEvent.OnEventFired -= OnDestructibleEnabledEvent;
    }

    private void OnDestructibleEnabledEvent(bool isEnabled)
    {
        _navMeshObstacle.enabled = !isEnabled;
    }

    public void TryDestroy()
    {
        CameraShakeManager.ShakeCamera(_shakeSettings);
        Destroy(gameObject);
    }
}
