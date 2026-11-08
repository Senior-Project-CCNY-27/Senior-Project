using Animancer;
using UnityEngine;

namespace MonsterStates
{
    [System.Serializable]
    public class CalmWanderState : State<MonsterController>
    {
        [SerializeField] private ClipTransition _animationClip;
        
        [SerializeField] private FloatRange _wanderRadiusRange = new FloatRange(3f, 5f);
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _rotateSpeed = 10f;

        private Vector3 _wanderCenter;
        
        private protected override void OnEnter()
        {
            _context.Animator.Play(_animationClip, 0.1f);

            _wanderCenter = _context.transform.position;
            Vector3 destination = _context.GetRandomVisibleWanderPoint(_wanderCenter, _wanderRadiusRange);
            _context.SetDestination(destination);
        }

        private protected override void OnExit()
        {
            _context.CancelPath();
        }

        private protected override void OnUpdate()
        {
            _context.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedUpdate()
        {
            _context.MoveTowardsDestination(_moveSpeed);
        }

        public override void OnDrawGizmosInPlayMode()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(_wanderCenter, Vector3.up, Mathf.Max(0f, _wanderRadiusRange.Max));
            UnityEditor.Handles.color = Color.blue;
            UnityEditor.Handles.DrawWireDisc(_wanderCenter, Vector3.up, Mathf.Max(0f, _wanderRadiusRange.Min));
#endif

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(_context.transform.position, _context.Destination);
            Gizmos.DrawWireSphere(_context.Destination, 0.2f);
        }
        
        private protected override State<MonsterController> GetTransition()
        {
            if (!_context.IsCurrentPathValid())
                return _context.CalmSuperState.IdleState;
            
            return null;
        }
    }
}
