using System;
using System.Linq;
using Animancer;
using NaughtyAttributes;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedChaseState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotateSpeed = 10f;
        
        [Header("Regular Hitbox")]
        [SerializeField] private float _regularHitboxRadius = 2f;
        
        [Header("Mouth Hitbox")]
        [SerializeField] private float _mouthHitboxRadius = 0.625f;

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
            _context.SetDestination(_targetTransform.position);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            Collider[] regularCollisions = Physics.OverlapSphere(_context.transform.position, _regularHitboxRadius);
            Collider[] mouthCollisions = Physics.OverlapSphere(_context.AgitatedSuperState.MouthHitboxTransform.position, _mouthHitboxRadius);
            foreach (Collider attackableCollider in regularCollisions.Concat(mouthCollisions))
            {
                if (attackableCollider.gameObject == _targetTransform.gameObject)
                {
                    _isTargetInAttackRange = true;
                    break;
                }

                if (attackableCollider.gameObject.CompareTag("Food"))
                {
                    _targetTransform = attackableCollider.gameObject.transform;
                    _isTargetInAttackRange = true;
                    break;
                }
            }
            
            _context.SetDestination(_targetTransform.position);
            _context.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowardsDestination(_moveSpeed);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (_targetTransform == null)
                return _context.AgitatedSuperState.RoamState;

            if (_isTargetInAttackRange)
            {
                // Ignore food in player's hands
                if (_targetTransform.TryGetComponent(out Pickup pickupComponent) && pickupComponent.IsPickedUp)
                {
                    _isTargetInAttackRange = false;
                    return null;
                }
                
                if (_targetTransform.gameObject.CompareTag("Food"))
                {
                    _context.Feed(_targetTransform.gameObject);
                    return null;
                }
                return _context.AgitatedSuperState.AttackState;
            }

            if (!_context.IsCurrentPathValid())
            {
                return new TransitionState<MonsterController>(
                    _stateMachine,
                    _context,
                    _context.Animator,
                    _context.AgitatedSuperState.AgitatedAnimationClip,
                    _context.AgitatedSuperState.ChaseState
                );
            }
            
            return null;
        }

        public override void OnDrawGizmosInPlayMode()
        {
            Gizmos.color = _isTargetInAttackRange ? Color.red : Color.blueViolet;
            Gizmos.DrawWireSphere(_context.transform.position, _regularHitboxRadius);
            Gizmos.DrawWireSphere(_context.AgitatedSuperState.MouthHitboxTransform.position, _mouthHitboxRadius);
        }
    }
}