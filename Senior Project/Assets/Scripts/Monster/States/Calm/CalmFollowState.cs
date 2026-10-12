using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmFollowState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _rotateSpeed = 10f;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            _context.GraduallyLookAt(_context.PlayerTransform.position, _rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowards(_context.PlayerTransform.position, _moveSpeed);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) <= _context.CalmSuperState.FollowStopDistance)
            {
                return _context.CalmSuperState.IdleState;
            }
            
            return null;
        }
    }
}