using System;
using MonsterStates;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class Destructible : MonoBehaviour
{
    [SerializeField, Required] private MonsterController _monsterController;
    private NavMeshObstacle _navMeshObstacle;
    
    [Header("Camera Shake")]
    [SerializeField] private CameraShakeSettings _shakeSettings;

    private void OnValidate()
    {
        _monsterController = FindObjectOfType<MonsterController>();
    }

    private void Awake()
    {
        if(_monsterController == null)
            _monsterController = FindObjectOfType<MonsterController>();
        
        _navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    private void Update()
    {
        AggressiveSuperState aggressiveSuperState = _monsterController.AggressiveSuperState;
        bool isAgitatedChasing = _monsterController.StateMachine.CurrentState == aggressiveSuperState &&
                                 aggressiveSuperState.SubStateMachine.CurrentState == aggressiveSuperState.ChaseState;
        _navMeshObstacle.enabled = !isAgitatedChasing;
    }

    public void Destroy()
    {
        CameraShakeManager.ShakeCamera(_shakeSettings);
        Destroy(gameObject);
    }
}
