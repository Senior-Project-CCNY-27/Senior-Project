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

        [field: SerializeField, ReadOnly] public Transform TargetTransform { get; private set; }
        private bool _isTargetInAttackRange = false;

        public void SetTarget(Transform targetTransform)
        {
            TargetTransform = targetTransform;
        }
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

            _isTargetInAttackRange = false;
            _context.SetDestination(TargetTransform.position);
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            HandleTargetPickedUp();
            CheckIfTargetInRange();

            _context.SetDestination(TargetTransform.position);
            _context.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowardsDestination(_moveSpeed);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (TargetTransform == null)
                return _context.AgitatedSuperState.RoamState;

            if (_isTargetInAttackRange)
            {
                // Ignore food in player's hands
                if (TargetTransform.TryGetComponent(out Pickup pickupComponent) && pickupComponent.IsPickedUp)
                {
                    _isTargetInAttackRange = false;
                    return null;
                }
                
                if (TargetTransform.gameObject.CompareTag("Food"))
                {
                    _context.Feed(TargetTransform.gameObject);
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
        
        public override void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out Destructible destructible))
                destructible.Destroy();
        }
        
        private void CheckIfTargetInRange()
        {
            Collider[] regularCollisions = Physics.OverlapSphere(_context.transform.position, _regularHitboxRadius);
            Collider[] mouthCollisions = Physics.OverlapSphere(_context.AgitatedSuperState.MouthHitboxTransform.position, _mouthHitboxRadius);
            foreach (Collider attackableCollider in regularCollisions.Concat(mouthCollisions))
            {
                if (attackableCollider.gameObject == TargetTransform.gameObject)
                {
                    _isTargetInAttackRange = true;
                    break;
                }

                if (attackableCollider.gameObject.CompareTag("Food"))
                {
                    TargetTransform = attackableCollider.gameObject.transform;
                    _isTargetInAttackRange = true;
                    break;
                }
            }
        }

        private void HandleTargetPickedUp()
        {
            // If player picks up the food, switch target to player
            if (TargetTransform.TryGetComponent(out Pickup pickup) && pickup.IsPickedUp)
                TargetTransform = pickup.PickupController.transform;
        }
    }
}