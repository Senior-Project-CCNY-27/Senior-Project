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
        [SerializeField, Range(0f, 1f)] private float _closeToPlayerWanderChance = 0.25f;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
        }

        private protected override void OnExit()
        {
            _context.CancelPath();
        }

        private protected override void OnUpdate()
        {
            _context.SetDestination(_context.PlayerTransform.position);
            _context.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowardsDestination(_moveSpeed);
        }

        public override void OnDrawGizmosInPlayMode()
        {
            if (_context.PlayerTransform == null)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_context.PlayerTransform.position, _context.CalmSuperState.FollowStopDistance);
            Gizmos.DrawLine(_context.transform.position, _context.PlayerTransform.position);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (Vector3.Distance(_context.transform.position, _context.PlayerTransform.position) <= _context.CalmSuperState.FollowStopDistance)
            {
                if (_context.CalmSuperState.SubStateMachine.PreviousState == _context.CalmSuperState.IdleState)
                    return _context.CalmSuperState.WanderState;
                
                if (RandomUtils.Chance(_closeToPlayerWanderChance))
                    return _context.CalmSuperState.WanderState;
                
                return _context.CalmSuperState.IdleState;
            }
            
            return null;
        }
    }
}
