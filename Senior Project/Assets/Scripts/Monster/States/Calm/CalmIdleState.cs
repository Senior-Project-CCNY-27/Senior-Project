using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmIdleState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;
        [SerializeField] private float _cooldown = 1f;

        private float _timer;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

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
            if (_timer >= _cooldown && Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) > _context.CalmSuperState.FollowStopDistance)
                return _context.CalmSuperState.FollowState;
            
            return null;
        }
    }
}