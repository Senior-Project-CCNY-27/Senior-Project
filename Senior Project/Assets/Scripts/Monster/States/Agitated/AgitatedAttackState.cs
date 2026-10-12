using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedAttackState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        private float _duration;
        private float _timer;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
            
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

        private protected override State<MonsterController> GetTransition()
        {
            if (_timer >= _duration)
            {
                return _context.AgitatedSuperState.ChaseState;
            }
            
            return null;
        }
    }
}