using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MonsterStates
{
    // Basic follow behavior; specialized follow states can choose a different reached transition.
    public class MonsterFollowState : State
    {
        [Header("References")]
        [SerializeField, Required] private MonsterController _monster;

        [Header("Animation")]
        [SerializeField] private ClipTransition _animationClip;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 1.5f;
        [SerializeField] private float _rotateSpeed = 10f;
        [SerializeField] private float _stopDistance = 7f;

        [Header("Transitions")]
        [SerializeField] private State _onReachedState;

        private protected bool HasReachedPlayer =>
            Vector3.Distance(_monster.transform.position, _monster.PlayerTransform.position) <= _stopDistance;
        private protected State ReachedState => _onReachedState;

        private protected override void OnEnter() => _monster.Animator.Play(_animationClip, 0.1f);
        private protected override void OnExit() => _monster.CancelPath();
        private protected override void OnTick()
        {
            _monster.SetDestination(_monster.PlayerTransform.position);
            _monster.RotateTowardsDestination(_rotateSpeed);
        }
        private protected override void OnFixedTick() => _monster.MoveTowardsDestination(_moveSpeed);

        private protected void OnDrawGizmos()
        {
            if (!ShouldDrawGizmos)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_monster.transform.position, _stopDistance);
        }

        private protected override State GetTransition() => HasReachedPlayer ? ReachedState : null;
    }
}
