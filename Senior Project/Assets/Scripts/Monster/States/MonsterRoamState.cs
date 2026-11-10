using System;
using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

namespace MonsterStates
{
    public class MonsterRoamState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onTargetFoundState;

        [Header("Callbacks")]
        [SerializeField] private UnityEvent<Transform> _onTargetDetected = new();

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Waypoints")]
        [SerializeField] private List<Transform> _waypoints = new List<Transform>();

        [Header("Detection")]
        [SerializeField] private float _innerDetectionRadius = 3f;
        [SerializeField] private float _losDetectionRadius = 10;
        [SerializeField] private float _losHalfAngleCone = 45;

        [Header("Movement")]
        [SerializeField] private float _rotateSpeed = 10f;
        [SerializeField] private float _roamMoveSpeed = 1.5f;
        
        private List<Transform> _availableWaypoints = new List<Transform>();
        private Transform _currentWaypoint;
        private Transform _targetTransform;
        
        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);
            
            _targetTransform = null;
            _availableWaypoints = new List<Transform>(_waypoints);
            
            PickAndMoveToRandomAvailableWaypoint();
        }

        private protected override void OnExit()
        {
            _monster.CancelPath();
        }

        private protected override void OnTick()
        {
            DetectTarget();

            if (!_monster.IsCurrentPathValid())
                PickAndMoveToRandomAvailableWaypoint();
            
            _monster.SetDestination(_monster.Destination); // To recalculate the path to the same destination in case the monster is moved
            _monster.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedTick()
        {
            _monster.MoveTowardsDestination(_roamMoveSpeed);
        }

        private protected override State GetTransition()
        {
            if (_targetTransform != null)
            {
                _onTargetDetected?.Invoke(_targetTransform);
                if (_onTargetFoundState is TransitionState transition)
                    transition.SetTarget(_targetTransform.gameObject);
                return _onTargetFoundState;
            }
            
            return null;
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = _targetTransform != null ? Color.red : Color.white;
            Gizmos.DrawWireSphere(_monster.transform.position, _innerDetectionRadius);
            CustomDebug.DrawWireSphereSectorIgnoringY(
                _monster.LOSOrigin.position,
                _monster.transform.forward,
                _losHalfAngleCone,
                _losDetectionRadius
            );
        }

        private void DetectTarget()
        {
            List<GameObject> targets = new List<GameObject>();
            
            List<GameObject> innerObjects = _monster.GetObjectsInRange(
                _innerDetectionRadius,
                _monster.transform.position,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedInnerObjects = _monster.GetVisibleObjects(innerObjects);
                
            List<GameObject> losObjects = _monster.GetObjectsInConeRange(
                _losDetectionRadius,
                _monster.LOSOrigin.position,
                _monster.transform.forward,
                _losHalfAngleCone,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedLOSObjects = _monster.GetVisibleObjects(losObjects);

            // Make sure interactables are food
            foreach (GameObject target in detectedInnerObjects.Concat(detectedLOSObjects))
            {
                if (target.layer == LayerMask.NameToLayer("Interactable") && !target.CompareTag("Food"))
                    continue;
                
                targets.Add(target);
            }

            if (targets.Count == 0)
            {
                _targetTransform = null;
                return;
            }
            
            targets = targets.OrderBy((target) => Vector3.Distance(_monster.transform.position, target.transform.position)).ToList();
            _targetTransform = targets[0].transform;
        }

        private void PickAndMoveToRandomAvailableWaypoint()
        {
            if (_availableWaypoints.Count == 0)
                _availableWaypoints = new List<Transform>(_waypoints);
            
            int randomIndex = UnityEngine.Random.Range(0, _availableWaypoints.Count);
            _currentWaypoint = _availableWaypoints[randomIndex];
            _availableWaypoints.RemoveAt(randomIndex);
            
            _monster.SetDestination(_currentWaypoint.position);
        }
    }
}
