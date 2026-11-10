using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterIdleState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onPlayerFarState;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Timing and Distance")]
        [SerializeField] private float _cooldown = 1f;
        [SerializeField] private float _followStartDistance = 7f;

        private float _timer;

        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);

            _timer = 0f;
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnTick()
        {
            _timer += Time.deltaTime;
        }

        private protected override void OnFixedTick()
        {
            
        }


        private protected override State GetTransition()
        {
            if (_timer > _cooldown && Vector3.Distance(_monster.transform.position, _monster.PlayerTransform.position) > _followStartDistance)
                return _onPlayerFarState;
            
            return null;
        }
    }
}
