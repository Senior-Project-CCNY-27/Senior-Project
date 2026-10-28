using System;
using Animancer;
using UnityEngine;

[System.Serializable]
public class TransitionState<TContext> : State<TContext> where TContext : MonoBehaviour
{
    private AnimancerComponent _animator;
    private ClipTransition _animationClip;
    private State<TContext> _nextState;
    private float _duration;
    private float _timer;

    private Action _onEnterCallback;
    private Action _onExitCallback;

    public TransitionState(
        StateMachine<TContext> stateMachine, 
        TContext context, 
        AnimancerComponent animator, 
        ClipTransition animationClip, 
        State<TContext> nextState,
        Action onEnterCallback = null,
        Action onExitCallback = null)
    {
        Init(stateMachine, context);
        _animator = animator;
        _animationClip = animationClip;
        _nextState = nextState;
        
        _onEnterCallback = onEnterCallback;
        _onExitCallback = onExitCallback;
    }
    
    private protected override void OnEnter()
    {
        _animator.Play(_animationClip, 0.1f);
        _duration = _animationClip.Length / _animationClip.Speed;
        _timer = 0f;
        
        _onEnterCallback?.Invoke();
    }

    private protected override void OnExit()
    {
        _onExitCallback?.Invoke();
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