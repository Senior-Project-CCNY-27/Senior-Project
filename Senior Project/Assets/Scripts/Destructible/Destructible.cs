using System;
using MonsterStates;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class Destructible : MonoBehaviour
{
    [SerializeField, Required] private MonsterController _monsterController;
    private NavMeshObstacle _navMeshObstacle;

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
        AgitatedSuperState agitatedSuperState = _monsterController.AgitatedSuperState;
        bool isAgitatedChasing = _monsterController.StateMachine.CurrentState == agitatedSuperState &&
                                 agitatedSuperState.SubStateMachine.CurrentState == agitatedSuperState.ChaseState;
        _navMeshObstacle.enabled = !isAgitatedChasing;
    }
}
