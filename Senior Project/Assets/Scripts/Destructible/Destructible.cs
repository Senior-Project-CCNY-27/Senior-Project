using MonsterStates;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class Destructible : MonoBehaviour
{
    [SerializeField] private MonsterChaseState _aggressiveChaseState;
    private NavMeshObstacle _navMeshObstacle;
    
    [Header("Camera Shake")]
    [SerializeField] private CameraShakeSettings _shakeSettings;

    private void Awake()
    {
        _navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    private void Update()
    {
        bool isChasing = _aggressiveChaseState != null && _aggressiveChaseState.IsRunning;
        _navMeshObstacle.enabled = !isChasing;
    }

    public void TryDestroy(MonsterChaseState chaseState)
    {
        if (chaseState == null || chaseState != _aggressiveChaseState || !chaseState.IsRunning)
            return;

        CameraShakeManager.ShakeCamera(_shakeSettings);
        Destroy(gameObject);
    }
}
