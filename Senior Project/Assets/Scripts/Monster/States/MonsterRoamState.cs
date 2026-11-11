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
        [SerializeField] private float _innerDetectionRadius = 2f;
        [SerializeField] private float _farLosDetectionRadius = 12.5f;
        [SerializeField] private float _farLosLateralRadius = 8f;
        [SerializeField] private float _farLosHalfAngleCone = 30f;
        [SerializeField] private float _nearLosDetectionRadius = 3.5f;
        [SerializeField] private float _nearLosLateralRadius = 7f;
        [SerializeField] private float _nearLosHalfAngleCone = 85f;

        [Header("Movement")]
        [SerializeField] private float _rotateSpeed = 10f;
        [SerializeField] private float _roamMoveSpeed = 1.5f;
        
        private List<Transform> _availableWaypoints = new List<Transform>();
        private Transform _currentWaypoint;
        private Transform _targetTransform;
        private bool _innerTargetDetected;
        private bool _farLosTargetDetected;
        private bool _nearLosTargetDetected;
        
        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);
            
            _targetTransform = null;
            
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

            if (_monster == null)
                return;

            Gizmos.color = _innerTargetDetected ? Color.red : Color.magenta;
            Gizmos.DrawWireSphere(_monster.transform.position, _innerDetectionRadius);

            Gizmos.color = _farLosTargetDetected ? Color.red : Color.green;
            CustomDebug.DrawWireEllipseSector(
                _monster.LosOrigin.position,
                _monster.transform.forward,
                _farLosDetectionRadius,
                _farLosLateralRadius,
                _farLosHalfAngleCone
            );

            Gizmos.color = _nearLosTargetDetected ? Color.red : Color.blue;
            CustomDebug.DrawWireEllipseSector(
                _monster.LosOrigin.position,
                _monster.transform.forward,
                _nearLosDetectionRadius,
                _nearLosLateralRadius,
                _nearLosHalfAngleCone
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
            _innerTargetDetected = detectedInnerObjects.Count > 0;
                
            List<GameObject> farLosObjects = _monster.GetObjectsInConeRange(
                _farLosDetectionRadius,
                _farLosLateralRadius,
                _monster.LosOrigin.position,
                _monster.transform.forward,
                _farLosHalfAngleCone,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedFarLOSObjects = _monster.GetVisibleObjects(farLosObjects);
            _farLosTargetDetected = detectedFarLOSObjects.Count > 0;
            
            List<GameObject> nearLosObjects = _monster.GetObjectsInConeRange(
                _nearLosDetectionRadius,
                _nearLosLateralRadius,
                _monster.LosOrigin.position,
                _monster.transform.forward,
                _nearLosHalfAngleCone,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedNearLOSObjects = _monster.GetVisibleObjects(nearLosObjects);
            _nearLosTargetDetected = detectedNearLOSObjects.Count > 0;

            var allDetectedObjects = detectedInnerObjects.Concat(detectedFarLOSObjects);
            allDetectedObjects = allDetectedObjects.Concat(detectedNearLOSObjects);
            
            // Make sure interactables are food and remove dupes
            foreach (GameObject target in allDetectedObjects.ToHashSet())
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
                RefillAvailableWaypoints();
            
            int randomIndex = UnityEngine.Random.Range(0, _availableWaypoints.Count);
            _currentWaypoint = _availableWaypoints[randomIndex];
            _availableWaypoints.RemoveAt(randomIndex);
            
            _monster.SetDestination(_currentWaypoint.position);
        }

        private void RefillAvailableWaypoints()
        {
            if (_waypoints == null)
            {
                _availableWaypoints = new();
                _availableWaypoints.Add(_monster.PlayerTransform);
                Debug.LogWarning($"{name} is missing waypoints, defaulting to roam towards player");
                return;
            }

            _availableWaypoints.Clear();
            foreach (Transform waypoint in _waypoints)
            {
                if(waypoint != null)
                    _availableWaypoints.Add(waypoint);
            }

            if (_availableWaypoints.Count == 0)
            {
                _availableWaypoints.Add(_monster.PlayerTransform);
                Debug.LogWarning($"{name} is missing waypoints, defaulting to roam towards player");
                return;
            }
        }
    }
}
