using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmCatchupState : State<MonsterController>
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
                return _context.CalmSuperState.IdleState;
            }
            
            return null;
        }
    }
}
