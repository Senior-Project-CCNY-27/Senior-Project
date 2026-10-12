using Animancer;
using UnityEngine;

public class TransitionState<TContext> : State<TContext> where TContext : MonoBehaviour
{
    private AnimancerComponent _animator;
    private ClipTransition _animationClip;
    private State<TContext> _nextState;
    private float _duration;
    private float _timer;

    public TransitionState(StateMachine<TContext> stateMachine, TContext context, AnimancerComponent animator, ClipTransition animationClip, State<TContext> nextState)
    {
        Init(stateMachine, context);
        _animator = animator;
        _animationClip = animationClip;
        _nextState = nextState;
    }
    
    private protected override void OnEnter()
    {
        _animator.Play(_animationClip, 0.1f);
        _duration = _animationClip.Length / _animationClip.Speed;
        _timer = 0f;
    }

    private protected override void OnExit()
    {
        
    }

    private protected override void OnUpdate()
    {
        _timer += Time.deltaTime;
    }

    private protected override void OnFixedUpdate()
    {

    }

    private protected override State<TContext> GetTransition()
    {
        if (_timer >= _duration)
        {
            return _nextState;
        }

        return null;
    }
}