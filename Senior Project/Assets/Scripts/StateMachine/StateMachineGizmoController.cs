using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(StateMachine))]
public class StateMachineGizmoController : MonoBehaviour
{
    [SerializeField, LabelText("Enable Gizmos When State Is Active"),
     OnValueChanged(nameof(RefreshGizmos))]
    private bool _enableGizmosWhenStateIsActive;

    private StateMachine _stateMachine;
    private readonly List<StateMachine> _stateMachines = new();
    private readonly List<State> _states = new();

    private void Awake() => CacheStateMachinesAndStates();

    private void LateUpdate()
    {
        if (Application.isPlaying)
            RefreshGizmos();
    }

    [Button("Disable Gizmos For All States", ButtonSizes.Large)]
    public void DisableGizmosForAllStates()
    {
        CacheStateMachinesAndStates();
        SetGizmosForAllStates(false);
    }

    private void RefreshGizmos()
    {
        if (_stateMachines.Count == 0 || _states.Count == 0)
            CacheStateMachinesAndStates();

        if (!Application.isPlaying)
        {
            if (!_enableGizmosWhenStateIsActive)
                SetGizmosForAllStates(false);
            return;
        }

        SetGizmosForAllStates(false);
        if (!_enableGizmosWhenStateIsActive)
            return;

        foreach (StateMachine stateMachine in _stateMachines)
        {
            if (stateMachine != null && stateMachine.IsRunning &&
                stateMachine.CurrentState != null && stateMachine.CurrentState.IsRunning)
            {
                stateMachine.CurrentState.SetShowGizmos(true);
            }
        }
    }

    private void CacheStateMachinesAndStates()
    {
        _stateMachine = GetComponent<StateMachine>();
        _stateMachines.Clear();
        _states.Clear();
        CacheStateMachinesAndStates(_stateMachine);
    }

    private void CacheStateMachinesAndStates(StateMachine stateMachine)
    {
        if (stateMachine == null)
            return;

        _stateMachines.Add(stateMachine);

        foreach (Transform child in stateMachine.transform)
        {
            if (child.TryGetComponent(out State state))
                _states.Add(state);

            if (child.TryGetComponent(out StateMachine subStateMachine))
                CacheStateMachinesAndStates(subStateMachine);
        }
    }

    private void SetGizmosForAllStates(bool enabled)
    {
        foreach (State state in _states)
            if (state != null)
                state.SetShowGizmos(enabled);
    }
}
