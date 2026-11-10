using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterStareState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onFinishedState;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Timing and Rotation")]
        [SerializeField] private FloatRange _durationRange = new FloatRange(2f, 2.5f);
        [SerializeField] private float _rotateSpeed = 5f;

        private float _timer;
        private float _duration;
        
        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);

            _timer = 0f;
            _duration = _durationRange.RandomValue();
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnTick()
        {
            _timer += Time.deltaTime;
            
            _monster.GraduallyLookAt(_monster.PlayerTransform.position, _rotateSpeed);
        }

        private protected override void OnFixedTick()
        {
            
        }


        private protected override State GetTransition()
        {
            if (_timer >= _duration)
                return _onFinishedState;
            
            return null;
        }
    }
}
