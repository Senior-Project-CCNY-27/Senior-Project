using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class StateMachine : MonoBehaviour
{
    [SerializeField, Required] private State _initialState;
    [SerializeField] private bool _autoTick = true;
    
    [ShowInInspector, ReadOnly] public State CurrentState { get; private set; }
    [ShowInInspector, ReadOnly] public State PreviousState { get; private set; }
    public bool HasTransitionedThisFrame { get; private set; }
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        foreach (Transform child in transform)
            if (child.TryGetComponent(out State state))
                state.Init(this);
    }

    private void Start()
    {
        if (_autoTick)
            StartMachine();
    }

    private void Update()
    {
        if (_autoTick)
            Tick();
    }

    private void FixedUpdate()
    {
        if (_autoTick)
            FixedTick();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_autoTick)
            CurrentState?.HandleCollision(collision);
    }

    private void OnDestroy() => StopMachine();

    public void DriveExternally() => _autoTick = false;

    public bool TryGetRunningState<T>(out T state) where T : State
    {
        state = null;
        if (!IsRunning || CurrentState == null || !CurrentState.IsRunning)
            return false;
        if (CurrentState is T runningState)
        {
            state = runningState;
            return true;
        }
        return CurrentState is SuperState superState
            && superState.SubStateMachine != null
            && superState.SubStateMachine.TryGetRunningState(out state);
    }

    public void StartMachine()
    {
        if (IsRunning)
            return;
        IsRunning = true;
        HasTransitionedThisFrame = false;
        ChangeState(_initialState, true);
    }

    public void StopMachine()
    {
        if (!IsRunning)
            return;
        ExitCurrentState();
        IsRunning = false;
    }

    public void ChangeState(State newState, bool forceChangeToSameState = false)
    {
        if (HasTransitionedThisFrame || newState == null ||
            (CurrentState == newState && !forceChangeToSameState))
            return;
        if (newState.Owner != this)
        {
            Debug.LogError($"State {newState.name} is not a child of this state machine. Check its hierarchy and Inspector references.", newState);
            return;
        }
        HasTransitionedThisFrame = true;
        PreviousState = CurrentState;
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Tick()
    {
        if (!IsRunning)
            return;
        HasTransitionedThisFrame = false;
        CurrentState?.Tick();
    }

    public void FixedTick()
    {
        if (IsRunning)
            CurrentState?.FixedTick();
    }

    public void ExitCurrentState()
    {
        CurrentState?.Exit();
        CurrentState = null;
        HasTransitionedThisFrame = false;
    }
}
