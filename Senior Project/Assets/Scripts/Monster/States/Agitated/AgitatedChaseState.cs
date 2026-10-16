using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedChaseState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [SerializeField] private Transform _attackHitBoxTransform;
        [SerializeField] private float _attackColliderRadius = 2f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotateSpeed = 10f;

        private bool _isTargetInAttackRange = false;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

            _isTargetInAttackRange = false;
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            _isTargetInAttackRange = Physics.OverlapSphere(_attackHitBoxTransform.position,
                _attackColliderRadius, LayerMask.GetMask("Player")).Length > 0;
            
            _context.NavMeshAgent.SetDestination(_context.PlayerTransform.position);
            _context.GraduallyLookAt(_context.transform.position + _context.NavMeshAgent.velocity, _rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveInDirection(_context.NavMeshAgent.velocity, _moveSpeed);
            _context.NavMeshAgent.nextPosition = _context.transform.position;
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (_isTargetInAttackRange)
            {
                return _context.AgitatedSuperState.AttackState;
            }
            
            return null;
        }

        public override void OnDrawGizmosInPlayMode()
        {
            Gizmos.color = _isTargetInAttackRange ? Color.red : Color.blueViolet;
            Gizmos.DrawWireSphere(_attackHitBoxTransform.position, _attackColliderRadius);
        }
    }
}