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

        private Transform _targetTransform;
        private bool _isTargetInAttackRange = false;

        public void SetTarget(Transform targetTransform)
        {
            _targetTransform = targetTransform;
        }
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

            _isTargetInAttackRange = false;
        }

        private protected override void OnExit()
        {
            _context.NavMeshAgent.ResetPath();
        }

        private protected override void OnUpdate()
        {
            Collider[] attackableColliders = Physics.OverlapSphere(_attackHitBoxTransform.position, _attackColliderRadius);
            foreach (Collider attackableCollider in attackableColliders)
            {
                if (attackableCollider.gameObject == _targetTransform.gameObject)
                {
                    _isTargetInAttackRange = true;
                    break;
                }
            }
            
            _context.NavMeshAgent.SetDestination(_targetTransform.position);
            _context.GraduallyLookAt(_context.transform.position + _context.NavMeshAgent.velocity, _rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveInDirection(_context.NavMeshAgent.velocity, _moveSpeed);
            _context.NavMeshAgent.nextPosition = _context.transform.position;
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (_targetTransform == null)
                return _context.AgitatedSuperState.RoamState;

            if (_isTargetInAttackRange)
            {
                if (_targetTransform.gameObject.layer == LayerMask.NameToLayer("Interactable"))
                {
                    _context.Feed();
                    return null;
                }
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