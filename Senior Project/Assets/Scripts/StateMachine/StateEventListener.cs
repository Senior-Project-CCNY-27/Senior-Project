using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Exposes a State's lifecycle callbacks as Inspector-configurable UnityEvents.
/// Place this on a state GameObject and assign that state and its owning machine.
/// </summary>
[DisallowMultipleComponent]
public sealed class StateEventListener : MonoBehaviour
{
    [Title("State")]
    [SerializeField, Required] private State _targetState;
    [SerializeField, Required] private StateMachine _stateMachine;

    [FoldoutGroup("On Enter"), SerializeField] private UnityEvent _onEnter = new();
    [FoldoutGroup("On Exit"), SerializeField] private UnityEvent _onExit = new();
    [FoldoutGroup("On Update"), SerializeField] private UnityEvent _onUpdate = new();
    [FoldoutGroup("On Fixed Update"), SerializeField] private UnityEvent _onFixedUpdate = new();
    [FoldoutGroup("On Collision Enter"), SerializeField] private UnityEvent _onCollisionEnter = new();

    private State _subscribedState;

    private void OnEnable()
    {
        Subscribe();

        // Also support enabling this component while its target state is already active.
        if (_targetState != null && _stateMachine != null &&
            _targetState.Owner == _stateMachine &&
            _stateMachine.CurrentState == _targetState && _targetState.IsRunning)
            InvokeIfConfigured(_onEnter);
    }

    private void OnDisable() => Unsubscribe();

    private void Start()
    {
        if (_targetState == null || _stateMachine == null)
        {
            Debug.LogError("Assign both a target State and its controlling StateMachine.", this);
            return;
        }

        if (_targetState.Owner != _stateMachine)
            Debug.LogError($"State '{_targetState.name}' is not owned by StateMachine '{_stateMachine.name}'. Check the references and hierarchy.", this);
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (_targetState == null)
            return;

        _subscribedState = _targetState;
        _subscribedState.Entered += HandleEnter;
        _subscribedState.Exited += HandleExit;
        _subscribedState.Updated += HandleUpdate;
        _subscribedState.FixedUpdated += HandleFixedUpdate;
        _subscribedState.CollisionEntered += HandleCollisionEnter;
    }

    private void Unsubscribe()
    {
        if (_subscribedState == null)
            return;

        _subscribedState.Entered -= HandleEnter;
        _subscribedState.Exited -= HandleExit;
        _subscribedState.Updated -= HandleUpdate;
        _subscribedState.FixedUpdated -= HandleFixedUpdate;
        _subscribedState.CollisionEntered -= HandleCollisionEnter;
        _subscribedState = null;
    }

    private bool IsTarget(State state) =>
        state == _targetState && _stateMachine != null && state.Owner == _stateMachine;

    private void HandleEnter(State state)
    {
        if (IsTarget(state))
            InvokeIfConfigured(_onEnter);
    }

    private void HandleExit(State state)
    {
        if (IsTarget(state))
            InvokeIfConfigured(_onExit);
    }

    private void HandleUpdate(State state)
    {
        if (IsTarget(state))
            InvokeIfConfigured(_onUpdate);
    }

    private void HandleFixedUpdate(State state)
    {
        if (IsTarget(state))
            InvokeIfConfigured(_onFixedUpdate);
    }

    private void HandleCollisionEnter(State state)
    {
        if (IsTarget(state))
            InvokeIfConfigured(_onCollisionEnter);
    }

    private static void InvokeIfConfigured(UnityEvent unityEvent) => unityEvent?.Invoke();
}
