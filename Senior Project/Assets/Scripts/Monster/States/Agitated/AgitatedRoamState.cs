using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using System.Linq;

namespace MonsterStates
{
    [System.Serializable]
    public class AgitatedRoamState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;

        [SerializeField] private List<Transform> _waypoints = new List<Transform>();
        [SerializeField] private float _innerDetectionRadius = 3f;
        [SerializeField] private float _losDetectionRadius = 10;
        [SerializeField] private float _losHalfAngleCone = 45;
        [SerializeField] private float _rotateSpeed = 10f;
        [SerializeField] private float _roamMoveSpeed = 1.5f;
        
        private List<Transform> _availableWaypoints = new List<Transform>();
        private Transform _currentWaypoint;
        private Transform _targetTransform;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
            
            _targetTransform = null;
            _availableWaypoints = new List<Transform>(_waypoints);
            
            PickAndMoveToRandomAvailableWaypoint();
        }

        private protected override void OnExit()
        {
            
        }

        private protected override void OnUpdate()
        {
            DetectTarget();

            if (!_context.IsCurrentPathValid())
                PickAndMoveToRandomAvailableWaypoint();
            
            _context.SetDestination(_context.Destination); // To recalculate the path to the same destination in case the monster is moved
            _context.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowardsDestination(_roamMoveSpeed);
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (_targetTransform != null)
            {
                _context.AgitatedSuperState.ChaseState.SetTarget(_targetTransform);
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
            Gizmos.color = _targetTransform != null ? Color.red : Color.white;
            Gizmos.DrawWireSphere(_context.transform.position, _innerDetectionRadius);
            CustomDebug.DrawWireSphereSectorIgnoringY(
                _context.LOSOrigin.position,
                _context.transform.forward,
                _losHalfAngleCone,
                _losDetectionRadius
            );
        }

        private void DetectTarget()
        {
            List<GameObject> targets = new List<GameObject>();
            
            List<GameObject> innerObjects = _context.GetObjectsInRange(
                _innerDetectionRadius,
                _context.transform.position,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedInnerObjects = _context.GetVisibleObjects(
                innerObjects,
                _context.transform.position,
                _innerDetectionRadius,
                LayerMask.GetMask("Player", "Interactable"),
                LayerMask.GetMask("Wall")
            );
                
            List<GameObject> losObjects = _context.GetObjectsInConeRange(
                _losDetectionRadius,
                _context.LOSOrigin.position,
                _context.transform.forward,
                _losHalfAngleCone,
                LayerMask.GetMask("Player", "Interactable") 
            );
            List<GameObject> detectedLOSObjects = _context.GetVisibleObjects(
                losObjects,
                _context.LOSOrigin.position,
                _losDetectionRadius,
                LayerMask.GetMask("Player", "Interactable"),
                LayerMask.GetMask("Wall")
            );

            foreach (GameObject innerObject in detectedInnerObjects)
            {
                if(!targets.Contains(innerObject))
                    targets.Add(innerObject);
            }
            
            foreach (GameObject losObject in detectedLOSObjects)
            {
                if(!targets.Contains(losObject))
                    targets.Add(losObject);
            }

            if (targets.Count == 0)
            {
                _targetTransform = null;
                return;
            }
            
            targets = targets.OrderBy((target) => Vector3.Distance(_context.transform.position, target.transform.position)).ToList();
            _targetTransform = targets[0].transform;
        }

        private void PickAndMoveToRandomAvailableWaypoint()
        {
            if (_availableWaypoints.Count == 0)
                _availableWaypoints = new List<Transform>(_waypoints);
            
            int randomIndex = UnityEngine.Random.Range(0, _availableWaypoints.Count);
            _currentWaypoint = _availableWaypoints[randomIndex];
            _availableWaypoints.RemoveAt(randomIndex);
            
            _context.SetDestination(_currentWaypoint.position);
        }
    }
}