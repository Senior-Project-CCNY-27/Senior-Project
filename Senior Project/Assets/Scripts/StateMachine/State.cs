using System;
using Sirenix.OdinInspector;
using UnityEngine;

// All attachable states share this non-generic type for Inspector references.
public abstract class State : SerializedMonoBehaviour
{
    public event Action<State> Entered;
    public event Action<State> Exited;
    public event Action<State> Updated;
    public event Action<State> FixedUpdated;
    public event Action<State> CollisionEntered;

    [Header("Debug")]
    [SerializeField] private bool _showGizmos;

    private protected StateMachine _stateMachine;
    public StateMachine Owner => _stateMachine;
    [field: SerializeField, ReadOnly] public bool IsRunning { get; private set; }
    private protected bool ShouldDrawGizmos => _showGizmos && (!Application.isPlaying || IsRunning);

    public void SetShowGizmos(bool enabled) => _showGizmos = enabled;

    public virtual void Init(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
        OnInit();
    }

    private protected virtual void OnInit() { }
    public void Enter()
    {
        IsRunning = true;
        OnEnter();
        Entered?.Invoke(this);
    }

    public void Exit()
    {
        IsRunning = false;
        OnExit();
        Exited?.Invoke(this);
    }

    public void Tick()
    {
        State destination = GetTransition();
        if (destination != null && !_stateMachine.HasTransitionedThisFrame)
        {
            _stateMachine.ChangeState(destination);
            return;
        }
        if (_stateMachine.CurrentState != this)
            return;
        OnTick();
        Updated?.Invoke(this);
    }

    public void FixedTick()
    {
        OnFixedTick();
        FixedUpdated?.Invoke(this);
    }

    public virtual void HandleCollision(Collision collision) => CollisionEntered?.Invoke(this);
    private protected abstract void OnEnter();
    private protected abstract void OnExit();
    private protected abstract void OnTick();
    private protected abstract void OnFixedTick();
    private protected virtual State GetTransition() => null;
}
