using System;
using System.Linq;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterChaseState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;
        [SerializeField, Required] private MonsterStateActions _stateActions;

        [Header("Transitions")]
        [SerializeField] private State _onTargetLostState;
        [SerializeField] private State _onTargetReachedState;
        [SerializeField] private State _onPathLostState;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotateSpeed = 10f;
        
        [Header("Regular Hitbox")]
        [SerializeField] private float _regularHitboxRadius = 2f;
        
        [Header("Mouth Hitbox")]
        [SerializeField] private float _mouthHitboxRadius = 0.625f;
        [SerializeField] private Transform _mouthHitboxTransform;

        [field: Header("Runtime")]
        [field: SerializeField, ReadOnly] public Transform TargetTransform { get; private set; }
        private bool _isTargetInAttackRange = false;

        public void SetTarget(Transform targetTransform)
        {
            TargetTransform = targetTransform;
        }
        
        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);

            _isTargetInAttackRange = false;
            _monster.SetDestination(TargetTransform.position);
        }

        private protected override void OnExit()
        {
            _monster.CancelPath();
        }

        private protected override void OnTick()
        {
            HandleTargetPickedUp();
            CheckIfTargetInRange();

            _monster.SetDestination(TargetTransform.position);
            _monster.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedTick()
        {
            _monster.MoveTowardsDestination(_moveSpeed);
        }
        
        private protected override State GetTransition()
        {
            if (TargetTransform == null)
                return _onTargetLostState;

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
                    _stateActions.Feed(TargetTransform.gameObject);
                    return null;
                }
                return _onTargetReachedState;
            }

            if (!_monster.IsCurrentPathValid())
            {
                return _onPathLostState;
            }
            
            return null;
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = _isTargetInAttackRange ? Color.red : Color.blueViolet;
            Gizmos.DrawWireSphere(_monster.transform.position, _regularHitboxRadius);
            Gizmos.DrawWireSphere(_mouthHitboxTransform.position, _mouthHitboxRadius);
        }
        
        public override void HandleCollision(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out Destructible destructible))
                destructible.TryDestroy(this);
        }
        
        private void CheckIfTargetInRange()
        {
            Collider[] regularCollisions = Physics.OverlapSphere(_monster.transform.position, _regularHitboxRadius);
            Collider[] mouthCollisions = Physics.OverlapSphere(_mouthHitboxTransform.position, _mouthHitboxRadius);
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
