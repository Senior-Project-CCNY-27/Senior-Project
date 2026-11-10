using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    public class MonsterWanderState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Transitions")]
        [SerializeField] private State _onPathFinishedState;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Wander Area")]
        [SerializeField] private FloatRange _wanderRadiusRange = new FloatRange(3f, 5f);

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _rotateSpeed = 10f;

        private Vector3 _wanderCenter;
        
        private protected override void OnEnter()
        {
            _monster.Animator.Play(_animationClip, 0.1f);

            _wanderCenter = _monster.PlayerTransform.position;
            Vector3 destination = _monster.GetRandomVisibleWanderPoint(_wanderCenter, _wanderRadiusRange);
            _monster.SetDestination(destination);
        }

        private protected override void OnExit()
        {
            _monster.CancelPath();
        }

        private protected override void OnTick()
        {
            _monster.RotateTowardsDestination(_rotateSpeed);
        }

        private protected override void OnFixedTick()
        {
            _monster.MoveTowardsDestination(_moveSpeed);
        }

        private void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_wanderCenter, _wanderRadiusRange.Max);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_wanderCenter, _wanderRadiusRange.Min);
        }
        
        private protected override State GetTransition()
        {
            if (!_monster.IsCurrentPathValid())
                return _onPathFinishedState;
            
            return null;
        }
    }
}
