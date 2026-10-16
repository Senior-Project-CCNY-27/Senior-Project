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

        [SerializeField] private float _innerDetectionRadius = 3f;
        [SerializeField] private float _losDetectionRadius = 10;
        [SerializeField] private float _losHalfAngleCone = 45;
        
        private bool _targetDetected;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);
            
            _targetDetected = false;
        }

        private protected override void OnExit()
        {

        }

        private protected override void OnUpdate()
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

        private protected override void OnFixedUpdate()
        {
           
        }

        private protected override State<MonsterController> GetTransition()
        {
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
    }
}