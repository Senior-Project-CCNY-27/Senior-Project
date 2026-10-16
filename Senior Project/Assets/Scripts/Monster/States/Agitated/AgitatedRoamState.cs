using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

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
        private bool _targetDetected;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
            
            _targetDetected = false;
            _availableWaypoints = new List<Transform>(_waypoints);
            
            PickAndMoveToRandomAvailableWaypoint();
        }

        private protected override void OnExit()
        {
            _context.NavMeshAgent.ResetPath();
        }

        private protected override void OnUpdate()
        {
            DetectTarget();

            if (Vector3.Distance(_currentWaypoint.position, _context.transform.position) < 0.05f)
                PickAndMoveToRandomAvailableWaypoint();
            
            _context.GraduallyLookAt(_context.transform.position + _context.NavMeshAgent.velocity, _rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveInDirection(_context.NavMeshAgent.velocity, _roamMoveSpeed);
            _context.NavMeshAgent.nextPosition = _context.transform.position;
        }

        private protected override State<MonsterController> GetTransition()
        {
            if (_targetDetected)
            {
                return new TransitionState<MonsterController>(
                    _stateMachine,
                    _context,
                    _context.Animator,
                    _context.AgitatedAnimationClip,
                    _context.AgitatedSuperState.ChaseState
                );
            }
            
            return null;
        }

        public override void OnDrawGizmosInPlayMode()
        {
            Gizmos.color = _targetDetected ? Color.red : Color.white;
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
            List<GameObject> innerObjects = _context.GetObjectsInRange(
                _innerDetectionRadius,
                _context.transform.position,
                LayerMask.GetMask("Player") 
            );
            List<GameObject> detectedInnerObjects = _context.GetVisibleObjects(
                innerObjects,
                _context.transform.position,
                _innerDetectionRadius,
                LayerMask.GetMask("Player"),
                LayerMask.GetMask("Wall")
            );
                
            List<GameObject> losObjects = _context.GetObjectsInConeRange(
                _losDetectionRadius,
                _context.LOSOrigin.position,
                _context.transform.forward,
                _losHalfAngleCone,
                LayerMask.GetMask("Player") 
            );
            List<GameObject> detectedLOSObjects = _context.GetVisibleObjects(
                losObjects,
                _context.LOSOrigin.position,
                _losDetectionRadius,
                LayerMask.GetMask("Player"),
                LayerMask.GetMask("Wall")
            );
            
            _targetDetected = detectedInnerObjects.Count > 0 || detectedLOSObjects.Count > 0;
        }

        private void PickAndMoveToRandomAvailableWaypoint()
        {
            if (_availableWaypoints.Count == 0)
                _availableWaypoints = new List<Transform>(_waypoints);
            
            int randomIndex = UnityEngine.Random.Range(0, _availableWaypoints.Count);
            _currentWaypoint = _availableWaypoints[randomIndex];
            _availableWaypoints.RemoveAt(randomIndex);
            
            _context.NavMeshAgent.SetDestination(_currentWaypoint.position);
        }
    }
}