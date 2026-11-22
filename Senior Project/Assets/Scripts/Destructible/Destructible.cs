using System;
using MonsterStates;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshObstacle))]
public class Destructible : MonoBehaviour
{
    [SerializeField, Required] private BoolScriptableEvent _onDestructibleEnabledEvent;
    private NavMeshObstacle _navMeshObstacle;

    [SerializeField] private UnityEvent<Vector3> _onDestroyed = new();

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

    public void TryDestroy(Vector3 hitPoint)
    {
        _onDestroyed?.Invoke(hitPoint);
        
        Destroy(gameObject);
    }
}
