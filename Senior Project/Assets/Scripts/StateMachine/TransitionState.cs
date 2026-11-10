using Animancer;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;

// Reusable timed transition. It has no dependency on a particular controller.
public class TransitionState : State
{
    [field: SerializeField, ReadOnly] public GameObject Target { get; private set; }
    
    [Header("Animation")]
    [SerializeField] private AnimancerComponent _animator;
    [SerializeField] private ClipTransition _animationClip;

    [Header("Transition")]
    [SerializeField] private State _destinationState;

    [Header("Timing")]
    [SerializeField] private float _durationOverride;

    [FoldoutGroup("On Enter"), SerializeField] private UnityEvent<GameObject> _onEnter = new();
    [FoldoutGroup("On Enter"), OdinSerialize] private SerializedMethodEvent _onEnterCalls = new();
    [FoldoutGroup("On Tick"), SerializeField] private UnityEvent<GameObject> _onTick = new();
    [FoldoutGroup("On Tick"), OdinSerialize] private SerializedMethodEvent _onTickCalls = new();
    [FoldoutGroup("On Fixed Tick"), SerializeField] private UnityEvent<GameObject> _onFixedTick = new();
    [FoldoutGroup("On Fixed Tick"), OdinSerialize] private SerializedMethodEvent _onFixedTickCalls = new();
    [FoldoutGroup("On Exit"), SerializeField] private UnityEvent<GameObject> _onExit = new();
    [FoldoutGroup("On Exit"), OdinSerialize] private SerializedMethodEvent _onExitCalls = new();

    private float _timer;
    private float _duration;

    public State DestinationState => _destinationState;
    public void SetTarget(GameObject target) => Target = target;

    private protected override void OnEnter()
    {
        _timer = 0f;
        _duration = _durationOverride > 0f ? _durationOverride :
            _animationClip != null && _animationClip.Speed != 0f
                ? _animationClip.Length / Mathf.Abs(_animationClip.Speed)
                : 0f;
        if (_animator != null && _animationClip != null)
            _animator.Play(_animationClip, 0.1f);
        _onEnter?.Invoke(Target);
        _onEnterCalls?.Invoke();
    }

    private protected override void OnExit()
    {
        _onExit?.Invoke(Target);
        _onExitCalls?.Invoke();
        Target = null;
    }

    private protected override void OnTick()
    {
        _timer += Time.deltaTime;
        _onTick?.Invoke(Target);
        _onTickCalls?.Invoke();
    }

    private protected override void OnFixedTick()
    {
        _onFixedTick?.Invoke(Target);
        _onFixedTickCalls?.Invoke();
    }

    private protected override State GetTransition() => _timer >= _duration ? _destinationState : null;
}
